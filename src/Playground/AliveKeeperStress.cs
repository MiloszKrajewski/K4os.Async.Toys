using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using K4os.Async.Toys;
using Microsoft.Extensions.Logging;

// ReSharper disable MethodSupportsCancellation

namespace Playground;

public class AliveKeeperStress
{
	private const int HANDLER_CONCURRENCY = 1000;
	private const int JOB_DURATION = 0;
	private const int RECEIVE_ROUNDTRIP = 0;
	private const int DELETE_ROUNDTRIP = 50;
	private const int BATCH_SIZE = 10;

	protected readonly ILogger<AliveKeeperStress> Log;

	public record Meta
	{
		public DateTime LastTouched { get; set; }
	}

	private readonly ConcurrentDictionary<Guid, Meta> _items = new();
	private int _handledTotal;
	private Stopwatch? _loopStarted;
	private int _touchConcurrent;
	private int _deleteConcurrent;
	private int _touchTotal;
	private static readonly TimeSpan TouchInterval = TimeSpan.FromSeconds(5);

	public AliveKeeperStress(ILoggerFactory loggerFactory)
	{
		Log = loggerFactory.CreateLogger<AliveKeeperStress>();
	}

	[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
	public async Task LotsOfUpkeepLoops()
	{
		var keeper = AliveKeeper.Create<Guid>(
			TouchMany,
			DeleteMany,
			logger: Log,
			settings: new AliveKeeperSettings {
				Concurrency = 16,
				TouchInterval = TouchInterval,
				RetryInterval = TimeSpan.FromSeconds(1),
				DeleteBatchSize = BATCH_SIZE,
				TouchBatchSize = BATCH_SIZE,
				RetryLimit = 3,
				TouchBatchDelay = TimeSpan.FromMilliseconds(100),
				SyncPolicy = AliveKeeperSyncPolicy.Alternating,
			});

		Log.LogInformation("Started...");

		var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		var sem = new SemaphoreSlim(HANDLER_CONCURRENCY);

		_handledTotal = 0;
		_loopStarted = Stopwatch.StartNew();

		Monitor(cts).Forget();

		while (!cts.IsCancellationRequested)
		{
			var guids = await ReceiveMany();
			Log.LogInformation("Adding {Count} items", guids.Length);

			foreach (var g in guids) keeper.Register(g);

			foreach (var g in guids)
			{
				await sem.WaitAsync();
				Task.Run(() => Handle(g, keeper, sem, cts.Token), cts.Token).Forget();
			}
		}

		Log.LogInformation("Waiting...");

		keeper.Dispose();

		Log.LogInformation("Stopped");
	}

	private async Task Monitor(CancellationTokenSource cts)
	{
		while (!cts.IsCancellationRequested)
		{
			var elapsed = _loopStarted?.Elapsed.TotalSeconds ?? double.MaxValue;
			var handled = _handledTotal;
			Log.LogWarning("Rate: {Rate:0.00}/s", handled / elapsed);
			await Task.Delay(1000, cts.Token);
		}
	}

	private async Task Handle(
		Guid job, IAliveKeeper<Guid> keeper, SemaphoreSlim sem, CancellationToken token)
	{
		try
		{
			await Task.Delay(JOB_DURATION, token);
			Interlocked.Increment(ref _handledTotal);
			var delete = keeper.Delete(job, CancellationToken.None);
			await delete;
			// delete.Forget();
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			keeper.Forget(job);
		}
		finally
		{
			sem.Release();
		}
	}

	private async Task<Guid[]> ReceiveMany()
	{
		await Task.Delay(RECEIVE_ROUNDTRIP);
		var guids = Enumerable.Range(0, BATCH_SIZE).Select(_ => Guid.NewGuid()).ToArray();
		foreach (var g in guids) _items.TryAdd(g, new Meta { LastTouched = DateTime.UtcNow });

		return guids;
	}

	private async Task<Guid[]> TouchMany(Guid[] guids)
	{
		var id = (uint)Guid.NewGuid().GetHashCode() % 1024;
		var concurrent = Interlocked.Increment(ref _touchConcurrent);
		var total = Interlocked.Add(ref _touchTotal, guids.Length);

		Log.LogInformation(
			"Touching {Count} items (id: {Id}, concurrent: {Concurrent}, total: {Total})",
			guids.Length, id, concurrent, total);
		await Task.Delay(DELETE_ROUNDTRIP);

		var now = DateTime.UtcNow;
		foreach (var g in guids)
		{
			if (!_items.TryGetValue(g, out var meta))
			{
				Log.LogWarning("Touching removed entry");
			}
			else
			{
				if (now - meta.LastTouched > TouchInterval*2)
					Log.LogError("Touching stale entry");
				meta.LastTouched = now;
			}
		}

		concurrent = Interlocked.Decrement(ref _touchConcurrent);

		Log.LogInformation(
			"Touched (id: {Id}, concurrent: {Concurrent}, total: {Total})", id, concurrent, total);

		return guids;
	}

	private async Task<Guid[]> DeleteMany(Guid[] guids)
	{
		var id = (uint)Guid.NewGuid().GetHashCode() % 1024;
		var concurrent = Interlocked.Increment(ref _deleteConcurrent);
		Log.LogInformation(
			"Deleting {Count} items (id: {Id}, concurrent: {Concurrent})", guids.Length, id,
			concurrent);

		await Task.Delay(DELETE_ROUNDTRIP);
		foreach (var g in guids)
		{
			_items.TryRemove(g, out _);
		}

		concurrent = Interlocked.Decrement(ref _deleteConcurrent);

		Log.LogInformation("Deleted (id: {Id}, concurrent: {Concurrent})", id, concurrent);

		return guids;
	}
}
