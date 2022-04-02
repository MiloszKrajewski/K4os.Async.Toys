using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Async.Toys
{
	/// <summary>Time source.</summary>
	public interface ITimeSource
	{
		/// <summary>Gets current time.</summary>
		DateTimeOffset Now { get; }
		
		/// <summary>
		/// Delays execution for given about of time.
		/// </summary>
		/// <param name="delay">Delay.</param>
		/// <param name="token">Cancellation token.</param>
		/// <returns>Delayed completion.</returns>
		Task Delay(TimeSpan delay, CancellationToken token);
	}

	/// <summary>Default time source.</summary>
	public class TimeSource: ITimeSource
	{
		/// <summary>Get default instance.</summary>
		public static readonly TimeSource Default = new();

		/// <summary>Created default instance.</summary>
		protected TimeSource() { }

		/// <inheritdoc />
		public DateTimeOffset Now => 
			DateTimeOffset.Now;

		/// <inheritdoc />
		public Task Delay(TimeSpan delay, CancellationToken token) =>
			Task.Delay(delay, token);
	}
}
