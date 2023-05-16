using System.Collections.Concurrent;
using BenchmarkDotNet.Attributes;
using K4os.Async.Toys;

namespace Benchmarks;

public class BatchBuilder1
{
	private const int Concurrency = 64;
	private readonly SemaphoreSlim _semaphore = new(Concurrency);
	private const int OpCount = 10000;
	private readonly ConcurrentBag<int> _bag = new();

	private readonly IBatchBuilder<int, int> _batchBuilder;

	public BatchBuilder1()
	{
		_batchBuilder = BatchBuilder.Create<int, int, int>(
			request => request,
			response => response,
			NoOpN,
			new BatchBuilderSettings { BatchSize = 10, Concurrency = Concurrency });
	}

	[Benchmark]
	public void Straight()
	{
		Task.WhenAll(Enumerable.Range(0, OpCount).Select(NoOp1)).Wait();
	}
	
	[Benchmark]
	public void AsBatch()
	{
		Task.WhenAll(Enumerable.Range(0, OpCount).Select(_batchBuilder.Request)).Wait();
	}

	private async Task<int[]> NoOpN(int[] requests)
	{
		await _semaphore.WaitAsync();
		await Delay();
		foreach (var request in requests) _bag.Add(request);
		_semaphore.Release();
		return requests;
	}

	private async Task NoOp1(int i)
	{
		await _semaphore.WaitAsync();
		await Delay();
		_bag.Add(i);
		_semaphore.Release();
	}

	private static Task Delay() => Task.Delay(1);
}
