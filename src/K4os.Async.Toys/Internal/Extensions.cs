using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

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

	public static T Required<T>(
		this T argument,
		[CallerArgumentExpression("argument")] string? argumentName = null) where T: class =>
		argument ?? throw new ArgumentNullException(argumentName ?? "<unknown>");

	public static T[] EmptyIfNull<T>(this T[]? argument) =>
		argument ?? Array.Empty<T>();

	[return: NotNullIfNotNull("fallback")]
	public static TValue? TryGetOrDefault<TKey, TValue>(
		this IDictionary<TKey, TValue> dictionary, TKey key, TValue? fallback = default) =>
		dictionary.TryGetValue(key, out var result) ? result : fallback;
}