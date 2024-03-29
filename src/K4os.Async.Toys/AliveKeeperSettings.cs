using System;

namespace K4os.Async.Toys;

/// <summary>
/// Synchronisation policy between touching and deleting.
/// <c>Safe</c> allows only one operation at a time, it is the safest option, but also the slowest.
/// <c>Alternating</c> allows touching and deleting to run in parallel, but only one type at a time,
/// this may reduce itself to <c>Safe</c> if both operations are scheduled roughly with same
/// frequency.
/// <c>Unrestricted</c> allows touching and deleting to run in parallel. This is the fastest option,
/// but may cause conflict. For example, item might be scheduled to be touched but gets deleted
/// in the meantime.
/// </summary>
public enum AliveKeeperSyncPolicy
{
	/// <summary>Safe, single operation at a time.</summary>
	Safe = 0,
	
	/// <summary>Multiple operations at a time, but only one type at a time.</summary>
	Alternating = 1,
	
	/// <summary>Full concurrency, multiple operations at a time, no restrictions.</summary>
	Unrestricted = 2,
}

/// <summary>
/// Alive keeper settings.
/// </summary>
public interface IAliveKeeperSettings
{
	/// <summary>Touch interval.</summary>
	TimeSpan TouchInterval { get; }

	/// <summary>Batch size for touching.</summary>
	int TouchBatchSize { get; }

	/// <summary>
	/// Maximum delay of touching (between scheduling and execution).
	/// </summary>
	TimeSpan TouchBatchDelay { get; }

	/// <summary>Batch size for deletion.</summary>
	int DeleteBatchSize { get; }

	/// <summary>Retry interval for deletion.</summary>
	TimeSpan RetryInterval { get; }

	/// <summary>Number of retries for deletion.</summary>
	int RetryLimit { get; }

	/// <summary>Level of concurrency for operations.</summary>
	int Concurrency { get; }
	
	/// <summary>
	/// Synchronisation policy between touching and deleting.
	/// <see cref="AliveKeeperSyncPolicy"/>
	/// </summary>
	AliveKeeperSyncPolicy SyncPolicy { get; }
}

/// <summary>
/// Alive keeper settings. 
/// </summary>
public class AliveKeeperSettings: IAliveKeeperSettings
{
	/// <inheritdoc />
	public TimeSpan TouchInterval { get; set; } = TimeSpan.FromSeconds(1);

	/// <inheritdoc />
	public int TouchBatchSize { get; set; } = 1;

	/// <inheritdoc />
	public TimeSpan TouchBatchDelay { get; set; } = TimeSpan.Zero;

	/// <inheritdoc />
	public int DeleteBatchSize { get; set; } = 1;

	/// <inheritdoc />
	public TimeSpan RetryInterval { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <inheritdoc />
	public int RetryLimit { get; set; } = 0;

	/// <inheritdoc />
	public int Concurrency { get; set; } = 1;

	/// <inheritdoc />
	public AliveKeeperSyncPolicy SyncPolicy { get; set; } = AliveKeeperSyncPolicy.Safe;
}