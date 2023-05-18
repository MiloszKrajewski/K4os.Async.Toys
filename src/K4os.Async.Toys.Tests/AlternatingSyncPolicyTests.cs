using K4os.Async.Toys.SyncPolicy;
using Xunit;
using Xunit.Abstractions;

namespace K4os.Async.Toys.Test;

public class AlternatingSyncPolicyTests
{
	public Action<string> Log { get; }
	private readonly AlternatingSyncPolicy _policy = new();

	public AlternatingSyncPolicyTests(ITestOutputHelper helper)
	{
		Log = helper.WriteLine;
	}

	[Fact]
	public void SecondTaskFromSameGroupIsAllowed()
	{
		var first = _policy.EnterDelete();
		Assert.True(first.Wait(1000));
	
		var second = _policy.EnterDelete();
		Assert.True(second.Wait(1000));
		
		_policy.LeaveDelete();
		_policy.LeaveDelete();
	}

	[Fact]
	public void TaskFromDifferentGroupHasToWait()
	{
		var first = _policy.EnterDelete();
		Assert.True(first.Wait(1000));
	
		var second = _policy.EnterTouch();
		Assert.False(second.Wait(1000));
		
		_policy.LeaveDelete();
		Assert.True(second.Wait(1000));
	}
	
	[Fact]
	public void WhenOtherGroupIsWaitingThenNewTasksAreNotAllowedIn()
	{
		var first = _policy.EnterDelete();
		Assert.True(first.Wait(1000));
	
		var second = _policy.EnterTouch();
		Assert.False(second.Wait(1000));

		var third = _policy.EnterDelete();
		Assert.False(third.Wait(1000));
		
		_policy.LeaveDelete();
		Assert.True(second.Wait(1000));
		Assert.False(third.Wait(1000));
		
		_policy.LeaveTouch();
		Assert.True(third.Wait(1000));
	}
	
	[Fact]
	public void AllAlreadyWaitingTasksAreAllowedInOnSwitch()
	{
		var first = _policy.EnterDelete();
		Assert.True(first.Wait(1000));
	
		var second = _policy.EnterTouch();
		Assert.False(second.Wait(1000));

		var third = _policy.EnterDelete();
		Assert.False(third.Wait(1000));
		
		var rest = Enumerable.Range(0, 10).Select(_ => _policy.EnterTouch()).ToArray();
		Assert.Equal(-1, Task.WaitAny(rest, 1000));
		
		_policy.LeaveDelete();
		Assert.True(second.Wait(1000));
		Assert.False(third.Wait(1000));
		Assert.True(Task.WaitAll(rest, 1000));

		_policy.LeaveTouch();
		Assert.False(third.Wait(1000));
		
		foreach (var _ in rest) _policy.LeaveTouch();
		Assert.True(third.Wait(1000));
	}

    private static void AssertNone(params Task[] tasks)
	{
		Assert.Equal(-1, Task.WaitAny(tasks, 1000));
	}
    
    private static void AssertAll(params Task[] tasks)
    {
	    Assert.True(Task.WaitAll(tasks, 1000));
    }
	
	[Fact]
	public void AllAlreadyWaitingTasksAreAllowedInOnSwitchButNotMore()
	{
		var first = _policy.EnterDelete();
		Assert.True(first.Wait(1000));
	
		var second = _policy.EnterTouch();
		Assert.False(second.Wait(1000));

		var third = _policy.EnterDelete();
		Assert.False(third.Wait(1000));
		
		var rest = Enumerable.Range(0, 10).Select(_ => _policy.EnterTouch()).ToArray();
		Assert.Equal(-1, Task.WaitAny(rest, 1000));
		
		var fourth = _policy.EnterDelete();
		Assert.False(third.Wait(1000));
		
		_policy.LeaveDelete();
		AssertAll(second);
		AssertAll(rest);
		AssertNone(third, fourth);
		
		var extra = _policy.EnterTouch();
		AssertNone(third, fourth, extra);

		_policy.LeaveTouch(); // just one...
		AssertNone(third, fourth, extra);
		
		foreach (var _ in rest) _policy.LeaveTouch(); // ...the rest
		AssertAll(third, fourth);
		AssertNone(extra);
		
		_policy.LeaveDelete();
		_policy.LeaveDelete();
		AssertAll(extra);
	}

	[Theory]
	[InlineData(1234, 100, 20)]
	[InlineData(1337, 100, 1000)]
	[InlineData(5678, 100, 1_000_000)]
	public void StressTest(int seed, double maxDuration, int maxCount)
	{
		var counters = new[] { 0, 0 };
		var random = new Random(seed);
		var tasks = Enumerable
			.Range(0, maxCount)
			.Select(_ => new { type = random.Next(), time = random.NextDouble() * maxDuration })
			.Select(kv => RunAction(counters, kv.type, kv.time));

		var maxTotalDuration = maxDuration * maxCount;
		var done = Task.WaitAll(tasks.ToArray(), TimeSpan.FromMilliseconds(maxTotalDuration));
		Assert.True(done);
	}

	private async Task RunAction(int[] counters, int rng, double time)
	{
		var type = rng % 2;
		var isDelete = type == 0;

		await Enter(isDelete);
		try
		{
			var thisCount = Interlocked.Increment(ref counters[type]);
			var otherCount = Volatile.Read(ref counters[1 - type]);
			if (otherCount != 0)
			{
				throw new InvalidOperationException(
					$"Other group is already running: {thisCount} vs {otherCount}");
			}
			await Task.Delay(TimeSpan.FromMilliseconds(time));
		}
		finally
		{
			Interlocked.Decrement(ref counters[type]);
			Leave(isDelete);
		}
	}

	private Task Enter(bool isDelete) => 
		isDelete ? _policy.EnterDelete() : _policy.EnterTouch();

	private void Leave(bool isDelete)
	{
		if (isDelete) _policy.LeaveDelete();
		else _policy.LeaveTouch();
	}
}
