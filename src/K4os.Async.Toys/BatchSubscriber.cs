using K4os.Async.Toys.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Async.Toys;

/// <summary>
/// Abstract base class for batch subscribers. Contains some static fields and helper methods.
/// Do not use directly.
/// </summary>
public abstract class BatchSubscriber
{
	/// <summary>Minimum interval between touch operations.</summary>
	protected static readonly TimeSpan MinimumTouchInterval = TimeSpan.FromMilliseconds(10);
	
	/// <summary>Minimum interval between retry attempts. This is to prevent retry storms.</summary>
	protected static readonly TimeSpan MinimumRetryInterval = TimeSpan.FromMilliseconds(10);
}

/// <summary>
/// A subscriber to any data source that supports batched polling.
/// </summary>
/// <typeparam name="TMessage">A type of a message being polled.</typeparam>
/// <typeparam name="TReceipt">A message receipt. Might be it's ID (not great) or unique
/// identifier of temporary ownership (better).</typeparam>
public class BatchSubscriber<TMessage, TReceipt>: BatchSubscriber, IDisposable
	where TMessage: notnull
	where TReceipt: notnull
{
	/// <summary>Logger.</summary>
	protected readonly ILogger Log;

	private readonly IBatchPoller<TMessage, TReceipt> _poller;
	private readonly Func<TMessage, CancellationToken, Task> _handler;
	private readonly SemaphoreSlim _semaphore;

	private readonly CancellationTokenSource _cancel;

	private readonly IAgent _agent;
	private readonly IAliveKeeper<TReceipt> _keeper;

	private readonly bool _asyncDelete;

	private record struct Burrito(TMessage Message, TReceipt Receipt);

	/// <summary>Creates a batch subscriber.</summary>
	/// <param name="poller">Data source poller.</param>
	/// <param name="handler">Message handler.</param>
	/// <param name="settings">Subscriber settings. Most like</param>
	/// <param name="token">Cancellation token.</param>
	/// <param name="logger">Logger.</param>
	public BatchSubscriber(
		IBatchPoller<TMessage, TReceipt> poller,
		Func<TMessage, CancellationToken, Task> handler,
		IBatchSubscriberSettings? settings = null,
		CancellationToken token = default,
		ILogger? logger = null)
	{
		Log = logger ?? NullLogger.Instance;
		
		settings = Validate(settings ?? new BatchSubscriberSettings());
		_asyncDelete = settings.AsynchronousDeletes;
		_poller = poller;
		_handler = handler;
		_cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
		_semaphore = new SemaphoreSlim(settings.HandlerCount);
		_keeper = AliveKeeper.Create<TReceipt>(
			TouchMany,
			DeleteMany,
			KeyOf,
			new AliveKeeperSettings {
				Concurrency = settings.BatchConcurrency,
				RetryLimit = settings.RetryLimit,
				RetryInterval = settings.RetryInterval,
				DeleteBatchSize = settings.DeleteBatchSize,
				TouchBatchSize = settings.TouchBatchSize,
				TouchBatchDelay = settings.TouchBatchDelay,
				TouchInterval = settings.TouchInterval,
				SyncPolicy = settings.AlternateBatches
					? AliveKeeperSyncPolicy.Alternating
					: AliveKeeperSyncPolicy.Unrestricted,
			},
			logger);
		_agent = Agent.Create(Loop, logger, _cancel.Token);
	}

	private static IBatchSubscriberSettings Validate(IBatchSubscriberSettings settings) =>
		new BatchSubscriberSettings {
			HandlerCount = settings.HandlerCount.NotLessThan(1),
			BatchConcurrency = settings.BatchConcurrency.NotLessThan(1),
			RetryLimit = settings.RetryLimit.NotLessThan(0),
			RetryInterval = settings.RetryInterval.NotLessThan(MinimumRetryInterval),
			DeleteBatchSize = settings.DeleteBatchSize.NotLessThan(1),
			TouchBatchSize = settings.TouchBatchSize.NotLessThan(1),
			TouchInterval = settings.TouchInterval.NotLessThan(MinimumTouchInterval),
			TouchBatchDelay = settings.TouchBatchDelay.NotLessThan(TimeSpan.Zero),
			AlternateBatches = settings.AlternateBatches,
		};

	/// <summary>Starts polling loop.</summary>
	public void Start() { _agent.Start(); }

	private string KeyOf(TReceipt message) =>
		_poller.IdentityOf(message);

	private Task<TReceipt[]> DeleteMany(TReceipt[] receipts) =>
		_poller.Delete(receipts, CancellationToken.None);

	private Task<TReceipt[]> TouchMany(TReceipt[] receipts) =>
		_poller.Touch(receipts, _cancel.Token);

	private static Task ForkOrWait<T>(
		bool wait,
		T state, Func<T, CancellationToken, Task> func, CancellationToken token)
	{
		if (wait) return func(state, token);

		Fork(state, func, token);
		return Task.CompletedTask;
	}
	
	private static void Fork<T>(
		T state, Func<T, CancellationToken, Task?> func, CancellationToken token)
	{
		Task.Run(() => func(state, token), token).Forget();
	}

	private async Task Loop(IAgentContext context)
	{
		var token = context.Token;

		while (!token.IsCancellationRequested)
		{
			var messages = await _poller.Receive(token);
			if (messages.Length == 0) continue;

			var burritos = messages
				.Select(m => new Burrito(m, _poller.ReceiptFor(m)))
				.ToArray();

			foreach (var b in burritos)
			{
				Register(b);
			}

			foreach (var b in burritos)
			{
				await _semaphore.WaitAsync(token);
				Fork(b, Handle, token);
			}
		}
	}

	private void Register(Burrito burrito) =>
		_keeper.Register(burrito.Receipt, _cancel.Token);

	private void Forget(Burrito burrito) =>
		_keeper.Forget(burrito.Receipt);

	private Task Complete(Burrito burrito, CancellationToken token) =>
		_keeper.Delete(burrito.Receipt, token);

	private async Task Handle(Burrito burrito, CancellationToken token)
	{
		try
		{
			await _handler(burrito.Message, token);
			await ForkOrWait(!_asyncDelete, burrito, Complete, token);
		}
		catch (Exception ex)
		{
			Log.LogError(ex, "Failed to handle message {Receipt}", burrito.Receipt);
			Forget(burrito);
		}
		finally
		{
			_semaphore.Release();
		}
	}

	/// <summary>
	/// Disposes the subscriber. Tries to handle all pending messages.
	/// </summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing) return;

		_agent.Dispose();
		_keeper.Dispose();
	}

	/// <summary>
	/// Disposes the subscriber. Tries to handle all pending messages.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
