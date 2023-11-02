using System;
using System.Linq;
using System.Threading.Channels;

#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace K4os.Async.Toys.Internal;

internal static class Extensions
{
	private static int Compare<T>(T value, T limit, IComparer<T>? comparer = null) =>
		(comparer ?? Comparer<T>.Default).Compare(value, limit);

	public static T NotLessThan<T>(this T value, T limit, IComparer<T>? comparer = null) =>
		Compare(value, limit, comparer) < 0 ? limit : value;
	
	public static T NotMoreThan<T>(this T value, T limit, IComparer<T>? comparer = null) =>
		Compare(value, limit, comparer) > 0 ? limit : value;

	public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
	{
		foreach (var item in sequence)
			action(item);
	}

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

	public static void Forget(this Task task)
	{
		task.ContinueWith(
			t => t.Exception, // clear exception so TPL stops complaining
			TaskContinuationOptions.NotOnRanToCompletion);
	}

	public static T Required<T>(this T argument, string argumentName) where T: class =>
		argument ?? throw new ArgumentNullException(argumentName);

	public static T[] EmptyIfNull<T>(this T[]? argument) =>
		argument ?? Array.Empty<T>();

	#if NETSTANDARD2_1_OR_GREATER || NET5_0_OR_GREATER
	[return: NotNullIfNotNull("fallback")]
	#endif
	public static TValue? TryGetOrDefault<TKey, TValue>(
		this IDictionary<TKey, TValue> dictionary, TKey key, TValue? fallback = default) =>
		dictionary.TryGetValue(key, out var result) ? result : fallback;
}
