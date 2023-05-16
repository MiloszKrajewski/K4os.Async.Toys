using System;

namespace K4os.Async.Toys;

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
}

/// <summary>
/// Alive keeper settings. 
/// </summary>
public class AliveKeeperSettings: IAliveKeeperSettings
{
	/// <inheritdoc />
	public TimeSpan TouchInterval { get; set; }

	/// <inheritdoc />
	public int TouchBatchSize { get; set; } = 1;

	/// <inheritdoc />
	public TimeSpan TouchBatchDelay { get; set; }

	/// <inheritdoc />
	public int DeleteBatchSize { get; set; } = 1;

	/// <inheritdoc />
	public TimeSpan RetryInterval { get; set; }

	/// <inheritdoc />
	public int RetryLimit { get; set; }

	/// <inheritdoc />
	public int Concurrency { get; set; } = 1;
}