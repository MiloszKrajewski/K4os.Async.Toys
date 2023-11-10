using System;
using K4os.Async.Toys.Internal;
using System.Linq;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Async.Toys;

/// <summary>Request batch builder.</summary>
/// <typeparam name="TRequest">Type of request.</typeparam>
/// <typeparam name="TResponse">Type of response.</typeparam>
public interface IBatchBuilder<in TRequest, TResponse>: IDisposable
{
	/// <summary>Execute a request inside a batch.</summary>
	/// <param name="request">A request.</param>
	/// <returns>Response.</returns>
	Task<TResponse> Request(TRequest request);
}

/// <summary>Request batch builder.</summary>
/// <typeparam name="TKey">Request id (used to match request with responses).</typeparam>
/// <typeparam name="TRequest">Type of request.</typeparam>
/// <typeparam name="TResponse">Type of response.</typeparam>
public class BatchBuilder<TKey, TRequest, TResponse>:
	IBatchBuilder<TRequest, TResponse>
	where TKey: notnull
{
	/// <summary>Logger.</summary>
	protected readonly ILogger Log;

	private readonly ITimeSource _time;

	private readonly Func<TRequest, TKey> _requestKey;
	private readonly Func<TResponse, TKey> _responseKey;
	private readonly Func<TRequest[], Task<TResponse[]>> _requestMany;

	private readonly Channel<Mailbox> _channel;
	private readonly SemaphoreSlim _semaphore;
	private readonly Task _loop;
	private readonly IBatchBuilderSettings _settings;

	/// <summary>Creates a batch builder.</summary>
	public BatchBuilder(
		Func<TRequest, TKey> requestKey,
		Func<TResponse, TKey> responseKey,
		Func<TRequest[], Task<TResponse[]>> requestMany,
		IBatchBuilderSettings? settings = null,
		ILogger? logger = null,
		ITimeSource? time = null)
	{
		Log = logger ?? NullLogger.Instance;
		_time = time ?? TimeSource.Default;
		_settings = settings = Validate(settings ?? new BatchBuilderSettings());

		_requestKey = requestKey.Required(nameof(requestKey));
		_responseKey = responseKey.Required(nameof(responseKey));
		_requestMany = requestMany.Required(nameof(requestMany));

		_channel = Channel.CreateUnbounded<Mailbox>(BatchBuilder.ChannelOptions);
		_semaphore = new SemaphoreSlim(settings.Concurrency);
		_loop = Task.Run(RequestLoop);
	}

	private static IBatchBuilderSettings Validate(IBatchBuilderSettings settings) =>
		new BatchBuilderSettings {
			BatchSize = settings.BatchSize.NotLessThan(1),
			BatchDelay = settings.BatchDelay.NotLessThan(TimeSpan.Zero),
			Concurrency = settings.Concurrency.NotLessThan(1),
		};

	/// <summary>Execute a request/call inside a batch.</summary>
	/// <param name="request">A request.</param>
	/// <returns>Response.</returns>
	public async Task<TResponse> Request(TRequest request)
	{
		var box = new Mailbox(request);
		await _channel.Writer.WriteAsync(box);
		return await box.Response.Task;
	}

	private async Task RequestLoop()
	{
		var reader = _channel.Reader;
		var length = _settings.BatchSize;
		var delay = _settings.BatchDelay;

		while (!reader.Completion.IsCompleted)
		{
			var requests = await reader.ReadManyAsync(delay, length, _time.Delay);
			if (requests is null) continue;

			await _semaphore.WaitAsync();
			RequestMany(requests).Forget();
		}
	}

	/// <summary>Gets key from request. It will be used to match requests with responses.</summary>
	/// <param name="request">Request.</param>
	/// <returns>Request key.</returns>
	protected TKey RequestKey(TRequest request) =>
		_requestKey(request);

	/// <summary>Gets key from response. It is used to match response with request.</summary>
	/// <param name="response">Response.</param>
	/// <returns>Response key.</returns>
	protected TKey ResponseKey(TResponse response) =>
		_responseKey(response);

	/// <summary>Action to send multiple requests.</summary>
	/// <param name="requests">Requests.</param>
	/// <returns>Array of responses.</returns>
	protected Task<TResponse[]> RequestMany(TRequest[] requests) =>
		_requestMany(requests);

	/// <summary>Delays execution.</summary>
	/// <param name="delay">Delay amount.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns></returns>
	protected Task Delay(TimeSpan delay, CancellationToken token) =>
		_time.Delay(delay, token);

	private async Task RequestMany(ICollection<Mailbox> requests)
	{
		try
		{
			if (requests.Count <= 0) return;

			var map = requests
				.GroupBy(r => RequestKey(r.Request))
				.ToDictionary(g => g.Key, g => g.ToArray());
			var keys = map.Keys.ToArray();

			try
			{
				var chosen = map
					.Select(kv => kv.Value[0].Request)
					.ToArray();
				var responses = await RequestMany(chosen);
				var handled = MarkAsComplete(responses, map);
				var missing = keys
					.Except(handled)
					.SelectMany(k => map.TryGetOrDefault(k).EmptyIfNull());
				MarkAsNotFound(missing);
			}
			catch (Exception e)
			{
				MarkAsFailed(requests, e);
			}
		}
		finally
		{
			_semaphore.Release();
		}
	}

	private static void MarkAsNotFound(IEnumerable<Mailbox> requests)
	{
		void NotFound(Mailbox box) =>
			box.Response.TrySetException(
				new KeyNotFoundException($"Missing response for {box.Request}"));

		requests.ForEach(NotFound);
	}

	private static void MarkAsFailed(IEnumerable<Mailbox> requests, Exception exception)
	{
		void Fail(Mailbox box) => box.Response.TrySetException(exception);
		requests.ForEach(Fail);
	}

	private IEnumerable<TKey> MarkAsComplete(
		IEnumerable<TResponse> responses,
		IDictionary<TKey, Mailbox[]> map)
	{
		foreach (var response in responses)
		{
			if (ReferenceEquals(response, null)) continue;

			void Complete(Mailbox box) => box.Response.TrySetResult(response);
			var key = ResponseKey(response);
			map.TryGetOrDefault(key)?.ForEach(Complete);
			yield return key;
		}
	}

	/// <summary>Disposes batch builder. Tries to handle all pending requests.</summary>
	/// <param name="disposing"><c>true</c> if triggered by user,
	/// <c>false</c> if triggered by GC.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing) return;

		_ = _channel.Writer.TryComplete();

		try
		{
			_loop.Wait();
		}
		finally
		{
			_semaphore.Dispose();
		}
	}

	/// <summary>Disposes batch builder. Tries to handle all pending requests.</summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	#region class Mailbox

	private class Mailbox
	{
		public TRequest Request { get; }
		public TaskCompletionSource<TResponse> Response { get; }

		public Mailbox(TRequest request)
		{
			Request = request;
			Response = new TaskCompletionSource<TResponse>(
				TaskCreationOptions.RunContinuationsAsynchronously);
		}
	}

	#endregion
}