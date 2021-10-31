﻿using System;
using System.Collections.Generic;
using K4os.Async.Toys.Internal;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Async.Toys
{
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
			var length = _settings.BatchSize;
			var delay = _settings.BatchDelay;
			
			while (!_channel.Reader.Completion.IsCompleted)
			{
				var requests = await ReadManyAsync(length, delay);
				if (requests is null) continue;

				await _semaphore.WaitAsync();
				RequestMany(requests).Forget();
			}
		}

		private async Task<List<Mailbox>?> ReadManyAsync(int length, TimeSpan delay)
		{
			var list = await _channel.Reader.ReadManyAsync(length);
			if (list is null || list.Count >= length || delay <= TimeSpan.Zero)
				return list;

			using var cancel = new CancellationTokenSource();
			using var window = Delay(delay, cancel.Token);
			await _channel.Reader.ReadManyMoreAsync(list, length, window);
			cancel.Cancel();
			return list;
		}

		protected TKey RequestKey(TRequest request) =>
			_requestKey(request);

		protected TKey ResponseKey(TResponse response) =>
			_responseKey(response);

		protected Task<TResponse[]> RequestMany(TRequest[] requests) =>
			_requestMany(requests);

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

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			_channel.Writer.Complete();

			try
			{
				_loop.Wait();
			}
			finally
			{
				_semaphore.Dispose();
			}
		}

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
}
