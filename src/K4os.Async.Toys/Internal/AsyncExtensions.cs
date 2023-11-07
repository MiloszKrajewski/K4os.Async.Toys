using System.Threading.Channels;

namespace K4os.Async.Toys.Internal;

/// <summary>
/// Async extensions.
/// </summary>
public static class AsyncExtensions
{
	/// <summary>
	/// Reads multiple items from channel reader.
	/// </summary>
	/// <param name="reader">Reader.</param>
	/// <param name="length">Maximum length.</param>
	/// <param name="token">Cancellation token.</param>
	/// <typeparam name="T">Type of item.</typeparam>
	/// <returns>List of read items.</returns>
	public static async Task<List<T>?> ReadManyAsync<T>(
		this ChannelReader<T> reader, int length = int.MaxValue,
		CancellationToken token = default)
	{
		var ready = await reader.WaitToReadAsync(token);
		if (!ready) return null;

		var list = default(List<T>);
		Drain(reader, ref list, ref length);

		return list;
	}

	/// <summary>
	/// Reads multiple items from channel reader, waiting for more items after initial item.
	/// </summary>
	/// <param name="reader">Reader.</param>
	/// <param name="delay">Delay.</param>
	/// <param name="length">Maximum length.</param>
	/// <param name="delayer">Delay method, <see cref="Task.Delay(TimeSpan,CancellationToken)"/>
	/// is used if not specified.</param>
	/// <param name="token">Cancellation token.</param>
	/// <typeparam name="T">Type of item.</typeparam>
	/// <returns>List of read items.</returns>
	public static async Task<List<T>?> ReadManyAsync<T>(
		this ChannelReader<T> reader, TimeSpan delay, 
		int length = int.MaxValue,
		Func<TimeSpan, CancellationToken, Task>? delayer = null,
		CancellationToken token = default)
	{
		var list = await reader.ReadManyAsync(length, token);
		if (list is null || list.Count >= length || delay <= TimeSpan.Zero)
			return list;

		using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
		using var window = (delayer ?? Task.Delay)(delay, cancel.Token);
		await reader.ReadManyMoreAsync(list, length, window);
		cancel.Cancel();
		return list;
	}

	/// <summary>
	/// Reads multiple items from channel reader, waiting for more items after initial item.
	/// </summary>
	/// <param name="reader">Reader.</param>
	/// <param name="delay">Delay.</param>
	/// <param name="length">Maximum length.</param>
	/// <param name="timeSource">Time source, system clock used in not specified.</param>
	/// <param name="token">Cancellation token.</param>
	/// <typeparam name="T">Type of item.</typeparam>
	/// <returns>List of read items.</returns>
	public static Task<List<T>?> ReadManyAsync<T>(
		this ChannelReader<T> reader, TimeSpan delay, 
		int length = int.MaxValue,
		ITimeSource? timeSource = null,
		CancellationToken token = default) =>
		reader.ReadManyAsync(delay, length, (timeSource ?? TimeSource.Default).Delay, token);
	
	private static async Task ReadManyMoreAsync<T>(
		this ChannelReader<T> reader, List<T> list, int length, Task window)
	{
		var completed = reader.Completion;
		length -= list.Count; // length left

		while (true)
		{
			Drain(reader, ref list!, ref length);
			if (length <= 0) break;

			var ready = reader.WaitToReadAsync().AsTask();
			var evt = await Task.WhenAny(window, completed, ready);
			if (evt != ready) break;
		}
	}

	private static void Drain<T>(
		ChannelReader<T> reader, ref List<T>? list, ref int length)
	{
		while (length > 0 && reader.TryRead(out var item))
		{
			(list ??= new List<T>()).Add(item);
			length--;
		}
	}

	/// <summary>
	/// Explicitly forgets about task, silencing potential error. Used for fire-and-forget.
	/// </summary>
	/// <param name="task">Task to be forgotten.</param>
	public static void Forget(this Task task)
	{
		task.ContinueWith(
			t => t.Exception, // clear exception so TPL stops complaining
			TaskContinuationOptions.NotOnRanToCompletion);
	}
}
