using System.Threading.Channels;
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
	private readonly IAliveKeeper<TReceipt> _keeper;
	private readonly CancellationTokenSource _cancel;

	private readonly IAgent _supervisor;
	private readonly Channel<Burrito> _burritos;
	private readonly IBatchSubscriberSettings _settings;
	
	private readonly SemaphoreSlim _runnerGate;
	private readonly SemaphoreSlim _pollerGate;

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

		_settings = settings = Validate(settings ?? new BatchSubscriberSettings());
		_poller = poller;
		_handler = handler;
		_cancel = CancellationTokenSource.CreateLinkedTokenSource(token);

		_burritos = Channel.CreateBounded<Burrito>(
			new BoundedChannelOptions(settings.InternalQueueSize) {
				SingleReader = false,
				SingleWriter = false,
				FullMode = BoundedChannelFullMode.Wait,
				AllowSynchronousContinuations = false,
			});

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
		
		_pollerGate = new SemaphoreSlim(settings.PollerCount);
		_runnerGate = new SemaphoreSlim(settings.HandlerCount);
		
		_supervisor = Agent.Create(Supervisor, logger, _cancel.Token);
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
			AsynchronousDeletes = settings.AsynchronousDeletes,
			InternalQueueSize = settings.InternalQueueSize.NotLessThan(1),
			PollerCount = settings.PollerCount.NotLessThan(1),
		};

	/// <summary>Starts polling loop.</summary>
	public void Start() { _supervisor.Start(); }

	private string KeyOf(TReceipt message) =>
		_poller.IdentityOf(message);

	private Task<TReceipt[]> DeleteMany(TReceipt[] receipts) =>
		_poller.Delete(receipts, CancellationToken.None);

	private Task<TReceipt[]> TouchMany(TReceipt[] receipts) =>
		_poller.Touch(receipts, _cancel.Token);
	
	private static async Task GatedFork(
		SemaphoreSlim gate, Func<CancellationToken, Task> func, CancellationToken token)
	{
		await gate.WaitAsync(token);
		Task
			.Run(() => func(token), token)
			.ContinueWith(_ => gate.Release(), CancellationToken.None)
			.Forget();
	}

	private static Task ForkOrWait(
		bool wait, Func<CancellationToken, Task> func, CancellationToken token)
	{
		if (wait) return func(token);

		Task.Run(() => func(token), token).Forget();
		return Task.CompletedTask;
	}

	private async Task Supervisor(IAgentContext context)
	{
		var token = context.Token;
		var interval = TimeSpan.FromSeconds(1);

		var poller = Agent.Create(Poller, Log, token);
		var runner = Agent.Create(Runner, Log, token);
		
		runner.Start();
		poller.Start();

		try
		{
			while (!token.IsCancellationRequested)
			{
				// we will need to periodically review number of pollers/runners later
				// for now this is just idle loop
				await Task.Delay(interval, token);
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			// ignore
		}
		finally
		{
			await poller.Done;
			_burritos.Writer.Complete();
			await runner.Done;
		}
	}

	private async Task<bool> Runner(IAgentContext context)
	{
		var token = context.Token;

		while (!token.IsCancellationRequested)
		{
			var burrito = await _burritos.Reader.ReadAsync(token);
			await GatedFork(_runnerGate, ct => HandleOne(burrito, ct), token);
		}

		return false;
	}

	private async Task<bool> Poller(IAgentContext context)
	{
		var token = context.Token;

		while (!token.IsCancellationRequested)
		{
			await GatedFork(_pollerGate, ReceiveMany, token);
		}

		return false;
	}
	
	private Burrito Register(TMessage message)
	{
		var receipt = _poller.ReceiptFor(message);
		_keeper.Register(receipt, _cancel.Token);
		return new Burrito(message, receipt);
	}

	private void Forget(Burrito burrito) =>
		_keeper.Forget(burrito.Receipt);

	private Task Complete(Burrito burrito, CancellationToken token) =>
		_keeper.Delete(burrito.Receipt, token);

	private async Task ReceiveMany(CancellationToken token)
	{
		var messages = await _poller.Receive(token);
		var burritos = messages.Select(Register).ToArray();

		foreach (var burrito in burritos)
			await _burritos.Writer.WriteAsync(burrito, token);
	}

	private async Task HandleOne(Burrito burrito, CancellationToken token)
	{
		var asyncDelete = _settings.AsynchronousDeletes;
		
		try
		{
			await _handler(burrito.Message, token);
			await ForkOrWait(!asyncDelete, ct => Complete(burrito, ct), token);
		}
		catch (Exception ex)
		{
			Log.LogError(ex, "Failed to handle message {Receipt}", burrito.Receipt);
			Forget(burrito);
		}
	}

	/// <summary>
	/// Disposes the subscriber. Tries to handle all pending messages.
	/// </summary>
	/// <param name="disposing"></param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing) return;

		_supervisor.Dispose();
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
