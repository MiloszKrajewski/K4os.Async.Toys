using System.Diagnostics;
using K4os.Async.Toys;
using Microsoft.Extensions.Logging;

// ReSharper disable MethodSupportsCancellation

namespace Playground;

public class SubscriberStressTest: IBatchPoller<SubscriberStressTest.Message, long>
{
	private const int TOTAL_TIME = 10;
	private const int HANDLER_CONCURRENCY = int.MaxValue;
	private const int BATCH_CONCURRENCY = int.MaxValue;
	private const int JOB_DURATION = 0;
	private const int RECEIVE_ROUNDTRIP = 0;
	private const int UPDATE_ROUNDTRIP = 0;
	private const int BATCH_SIZE = 10;
	
	private static readonly TimeSpan TOUCH_INTERVAL = TimeSpan.FromSeconds(5);

	protected readonly ILogger Log;

	public record Message(Guid Id, long Receipt);

	private int _handledTotal;

	public SubscriberStressTest(ILoggerFactory loggerFactory)
	{
		Log = loggerFactory.CreateLogger<SubscriberStressTest>();
	}

	public async Task ConstantStreamOfMessages(TimeSpan duration)
	{
		var subscriber = new BatchSubscriber<Message, long>(
			this,
			HandleMessage,
			new BatchSubscriberSettings {
				BatchConcurrency = BATCH_CONCURRENCY,
				TouchInterval = TOUCH_INTERVAL,
				RetryInterval = TimeSpan.FromSeconds(1),
				RetryLimit = 5,
				TouchBatchSize = BATCH_SIZE,
				DeleteBatchSize = BATCH_SIZE,
				TouchBatchDelay = TimeSpan.FromMilliseconds(100),
				AlternateBatches = true,
				AsynchronousDeletes = true,
				HandlerCount = HANDLER_CONCURRENCY,
			});

		var limit = Task.Delay(TimeSpan.FromSeconds(TOTAL_TIME));

		_handledTotal = 0;
		var elapsed = Stopwatch.StartNew();
		subscriber.Start();
		
		while (!limit.IsCompleted)
		{
			await Task.WhenAny(Task.Delay(1000), limit);
			var total = _handledTotal;
			var rate = total / elapsed.Elapsed.TotalSeconds;
			Log.LogInformation("Handled {Count} messages / {Rate:0.00}/s", total, rate);
		}
	
		Log.LogInformation("Disposing...");
		subscriber.Dispose();
		Log.LogInformation("Disposed");
	}

	private async Task HandleMessage(Message message, CancellationToken token)
	{
		await Task.Delay(JOB_DURATION, token);
		Interlocked.Increment(ref _handledTotal);
	}

	public long ReceiptFor(Message message) => message.Receipt;
	public string IdentityOf(long receipt) => receipt.ToString();

	private long _currentReceipt;

	public async Task<Message[]> Receive(CancellationToken token)
	{
		await Task.Delay(RECEIVE_ROUNDTRIP);
		var messages = Enumerable
			.Range(0, BATCH_SIZE)
			.Select(_ => new Message(Guid.NewGuid(), Interlocked.Increment(ref _currentReceipt)))
			.ToArray();
		// Log.LogInformation("Received {Count} messages", messages.Length);
		return messages;
	}

	public async Task<long[]> Delete(long[] receipts, CancellationToken token)
	{
		await Task.Delay(UPDATE_ROUNDTRIP);
		// Log.LogInformation("Deleted {Count} messages", receipts.Length);
		return receipts;
	}

	public async Task<long[]> Touch(long[] receipts, CancellationToken token)
	{
		await Task.Delay(UPDATE_ROUNDTRIP);
		// Log.LogWarning("Touched {Count} messages", receipts.Length);
		return receipts;
	}
}
