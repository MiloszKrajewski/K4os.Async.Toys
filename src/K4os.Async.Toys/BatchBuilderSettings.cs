using System;

namespace K4os.Async.Toys
{
	public interface IBatchBuilderSettings
	{
		public int BatchSize { get; }
		public TimeSpan BatchDelay { get; }
		public int Concurrency { get; }
	}

	public class BatchBuilderSettings: IBatchBuilderSettings
	{
		public int BatchSize { get; set; } = 1;
		public TimeSpan BatchDelay { get; set; } = TimeSpan.Zero;
		public int Concurrency { get; set; } = 1;
	}
}
