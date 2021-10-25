using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Async.Toys
{
	public interface ITimeSource
	{
		DateTimeOffset Now { get; }
		Task Delay(TimeSpan delay, CancellationToken token);
	}

	public class TimeSource: ITimeSource
	{
		public static readonly TimeSource Default = new();

		protected TimeSource() { }

		public DateTimeOffset Now => 
			DateTimeOffset.Now;

		public Task Delay(TimeSpan delay, CancellationToken token) =>
			Task.Delay(delay, token);
	}
}
