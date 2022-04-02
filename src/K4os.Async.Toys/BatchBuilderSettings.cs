using System;

namespace K4os.Async.Toys;

/// <summary>
/// Batch builder settings.
/// </summary>
public interface IBatchBuilderSettings
{
	/// <summary>Maximum batch size.</summary>
	public int BatchSize { get; }
		
	/// <summary>Maximum batch delay.</summary>
	public TimeSpan BatchDelay { get; }
		
	/// <summary>Concurrency level.</summary>
	public int Concurrency { get; }
}

/// <inheritdoc />
public class BatchBuilderSettings: IBatchBuilderSettings
{
	/// <inheritdoc />
	public int BatchSize { get; set; } = 1;

	/// <inheritdoc />
	public TimeSpan BatchDelay { get; set; } = TimeSpan.Zero;

	/// <inheritdoc />
	public int Concurrency { get; set; } = 1;
}