namespace K4os.Async.Toys;

/// <summary>
/// Settings for <see cref="BatchSubscriber{TMessage,TReceipt}"/>. See <see cref="BatchSubscriberSettings"/>
/// </summary>
public interface IBatchSubscriberSettings
{
	/// <summary>Number of handlers to be used to process messages concurrently.</summary>
	int HandlerCount { get; set; }
	
	/// <summary>
	/// Retry limit for both Touch and Delete requests. Setting it to 0 will disable retries.
	/// </summary>
	int RetryLimit { get; set; }
	
	/// <summary>
	/// Retry interval for both Touch and Delete requests. Not relevant if <see cref="RetryLimit"/> is 0.
	/// </summary>
	TimeSpan RetryInterval { get; set; }
	
	/// <summary>Number of concurrent batches to be sent for both Touch and Delete requests.
	/// Setting it to 1 will make it sequential. See <see cref="DeleteBatchSize"/> and
	/// <see cref="TouchBatchSize"/>.</summary>
	int BatchConcurrency { get; set; }
	
	/// <summary>Batch size for Delete requests. See <see cref="BatchConcurrency"/>.</summary>
	int DeleteBatchSize { get; set; }
	
	/// <summary>Batch size for Touch requests. See <see cref="BatchConcurrency"/>.</summary>
	int TouchBatchSize { get; set; }
	
	/// <summary>
	/// Maximum delay between scheduling and executing Touch requests. Bigger values will
	/// batching more optimal, but will also increase latency. Keep it at 0 if unsure.
	/// </summary>
	TimeSpan TouchBatchDelay { get; set; }
	
	/// <summary>
	/// Minimum interval between Touch requests. Lower values are safer, while higher values
	/// are reducing network traffic. Half of claim lifespan is a good starting point.
	/// </summary>
	TimeSpan TouchInterval { get; set; }
	
	/// <summary>
	/// Makes sure that Touch and Delete requests do not overlap. This is safer but less
	/// effective option. For example, it is possible that item might be touched and
	/// deleted at roughly the same time. Depends on how underlying data source works.
	/// </summary>
	bool AlternateBatches { get; set; }
	
	/// <summary>
	/// Performs deletes asynchronously. This is more aggressive approach, as processing
	/// messages will not ensure everything is deleted, but give a little bit higher throughput.
	/// </summary>
	bool AsynchronousDeletes { get; set; }

	/// <summary>
	/// Length of internal queue. Increasing it may help smoothing processing speed, but
	/// using 1 is also fine. 
	/// </summary>
	int InternalQueueSize { get; set; }

	/// <summary>
	/// Number of source date pollers. Affects how quickly new job as acquired, increase it only
	/// if your runners are idle, but not overdo it as all messages which are polled but not handled
	/// yet will need to be tracked and touched periodically. Default value is usually good enough.
	/// </summary>
	int PollerCount { get; set; }
}

/// <summary>
/// Settings for <see cref="BatchSubscriber{TMessage,TReceipt}"/>.
/// </summary>
public class BatchSubscriberSettings: IBatchSubscriberSettings
{
	/// <inheritdoc />
	public int HandlerCount { get; set; } = 1;

	/// <inheritdoc />
	public int BatchConcurrency { get; set; } = 1;

	/// <inheritdoc />
	public int RetryLimit { get; set; } = 0;

	/// <inheritdoc />
	public TimeSpan RetryInterval { get; set; } = TimeSpan.Zero;

	/// <inheritdoc />
	public int DeleteBatchSize { get; set; } = 1;

	/// <inheritdoc />
	public int TouchBatchSize { get; set; } = 1;

	/// <inheritdoc />
	public TimeSpan TouchBatchDelay { get; set; } = TimeSpan.Zero;

	/// <inheritdoc />
	public TimeSpan TouchInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <inheritdoc />
	public bool AlternateBatches { get; set; } = true;

	/// <inheritdoc />
	public bool AsynchronousDeletes { get; set; } = false;

	/// <inheritdoc />
	public int InternalQueueSize { get; set; } = 1;

	/// <inheritdoc />
	public int PollerCount { get; set; } = 1;
}
