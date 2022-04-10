using System;
using System.Threading;
using Xunit;

namespace K4os.Async.Toys.Test
{
	public class ManualResetSignalTests
	{
		[Fact]
		public void ItIsNotSignaled()
		{
			var signal = new ManualResetSignal();
			Assert.False(signal.IsSet);
		}

		[Fact]
		public void ItIsSignaledWhenSignaled()
		{
			var signal = new ManualResetSignal();
			signal.Set();
			Assert.True(signal.IsSet);
			Assert.True(signal.WaitAsync().IsCompleted);
		}

		[Fact]
		public void AllTasksAwaitingAreNotified()
		{
			var signal = new ManualResetSignal();
			var task = signal.WaitAsync();

			Assert.False(task.IsCompleted);

			signal.Set();

			Assert.True(task.IsCompleted);
		}
		
		[Fact]
		public void ItReturnsTrueWhenTaskIsFinished()
		{
			var signal = new ManualResetSignal();
			var task = signal.WaitAsync(CancellationToken.None);

			Assert.False(task.IsCompleted);

			signal.Set();

			Assert.True(task.IsCompleted);
			Assert.True(task.Result);
		}
		
		[Fact]
		public void ItReturnsFalseWhenWaitingIsCancelled()
		{
			var signal = new ManualResetSignal();
			var cancel = new CancellationTokenSource();
			var task = signal.WaitAsync(cancel.Token);

			Assert.False(task.IsCompleted);

			cancel.Cancel();

			Assert.True(task.IsCompleted);
			Assert.False(task.Result);
		}
		
		[Fact]
		public void ItReturnsFalseWhenWaitingTimesOut()
		{
			var signal = new ManualResetSignal();
			var task = signal.WaitAsync(TimeSpan.FromSeconds(1));

			var counter = 0;
			while (counter < 15)
			{
				if (task.IsCompleted) break;
				Thread.Sleep(TimeSpan.FromMilliseconds(100));
				counter++;
			}
			
			Assert.True(task.IsCompleted);
			Assert.False(task.Result);
			Assert.True(counter is >= 8 and <= 12); // +-200ms 
		}
	}
}
