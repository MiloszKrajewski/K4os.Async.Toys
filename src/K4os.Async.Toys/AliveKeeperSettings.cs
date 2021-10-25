using System;

namespace K4os.Async.Toys
{
	public interface IAliveKeeperSettings
	{
		TimeSpan TouchInterval { get; }
		int TouchBatchSize { get; }
		TimeSpan TouchBatchDelay { get; }
		int DeleteBatchSize { get; }
		TimeSpan RetryInterval { get; }
		int RetryLimit { get; }
	}

	public class AliveKeeperSettings: IAliveKeeperSettings
	{
		public TimeSpan TouchInterval { get; set; }
		public int TouchBatchSize { get; set; } = 1;
		public TimeSpan TouchBatchDelay { get; set; }
		public int DeleteBatchSize { get; set; } = 1;
		public TimeSpan RetryInterval { get; set; }
		public int RetryLimit { get; set; }
	}
}
