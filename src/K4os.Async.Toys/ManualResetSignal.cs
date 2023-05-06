using System;
using System.Threading;
using System.Threading.Tasks;

namespace K4os.Async.Toys;

/// <summary>
/// Simple implementation of ManualResetEvent with asynchronous <see cref="WaitAsync()"/>.
/// </summary>
public class ManualResetSignal
{
	private readonly object _lock = new();
	private TaskCompletionSource<bool> _tcs = NewTaskCompletionSource();
	private readonly ITimeSource? _timeSource;

	private static TaskCompletionSource<bool> NewTaskCompletionSource() =>
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>
	/// Creates new instance of <see cref="ManualResetSignal"/>.
	/// </summary>
	public ManualResetSignal() { }

	/// <summary>
	/// Creates new instance of <see cref="ManualResetSignal"/> with specified <see cref="ITimeSource"/>.
	/// </summary>
	/// <param name="timeSource">Time source for executing delay if needed.</param>
	public ManualResetSignal(ITimeSource? timeSource) =>
		_timeSource = timeSource;

	private Task<bool> GetTask()
	{
		lock (_lock) return _tcs.Task;
	}

	/// <summary>
	/// Sets the event to signaled state.
	/// </summary>
	public void Set()
	{
		lock (_lock) _tcs.TrySetResult(true);
	}

	/// <summary>
	/// Sets the event to non-signaled state.
	/// </summary>
	public void Reset()
	{
		lock (_lock)
		{
			if (_tcs.Task.IsCompleted)
				_tcs = NewTaskCompletionSource();
		}
	}

	/// <summary>
	/// Checks if signal is set.
	/// </summary>
	public bool IsSet
	{
		get
		{
			lock (_lock) return _tcs.Task.IsCompleted;
		}
	}

	/// <summary>
	/// Waits for the event to be signaled.
	/// </summary>
	/// <returns>Task to be awaited.</returns>
	public Task WaitAsync() => GetTask();

	/// <summary>
	/// Waits for the event to be signaled.
	/// </summary>
	/// <param name="token">CancellationToken.</param>
	/// <returns>Task to be awaited.</returns>
	public Task<bool> WaitAsync(CancellationToken token) =>
		WaitAsync(token, Timeout.InfiniteTimeSpan);

	/// <summary>
	/// Waits for the event to be signaled.
	/// </summary>
	/// <param name="timeout">Timeout.</param>
	/// <returns>Task to be awaited.</returns>
	public Task<bool> WaitAsync(TimeSpan timeout) =>
		WaitAsync(CancellationToken.None, timeout);

	/// <summary>
	/// Waits for the event to be signaled.
	/// </summary>
	/// <param name="token">CancellationToken.</param>
	/// <param name="timeout">Timeout.</param>
	/// <returns>Task to be awaited.</returns>
	public Task<bool> WaitAsync(CancellationToken token, TimeSpan timeout)
	{
		var task = GetTask();

		if (task.IsCompleted) return task;

		var canBeCancelled =
			token.CanBeCanceled ||
			timeout != Timeout.InfiniteTimeSpan;

		return canBeCancelled ? WaitAsync(task, token, timeout) : task;
	}

	private async Task<bool> WaitAsync(
		Task task, CancellationToken token, TimeSpan timeout)
	{
		var combined = CancellationTokenSource.CreateLinkedTokenSource(token);
		try
		{
			var done = await Task.WhenAny(task, Delay(timeout, combined.Token));
			return done == task;
		}
		finally
		{
			combined.Cancel();
		}
	}

	private Task Delay(TimeSpan timeout, CancellationToken token) =>
		_timeSource is null
			? Task.Delay(timeout, token)
			: _timeSource.Delay(timeout, token);
}
