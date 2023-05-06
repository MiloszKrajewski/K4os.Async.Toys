using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace K4os.Async.Toys;

/// <summary>
/// Static factory of <see cref="IAliveKeeper{T}"/> classes.
/// </summary>
public static class AliveKeeper
{
	/// <summary>
	/// Creates <see cref="IAliveKeeper{T}"/> that will keep alive
	/// items of type <typeparamref name="T"/>
	/// </summary>
	/// <param name="touchAction">Action to touch item.</param>
	/// <param name="deleteAction">Action to delete item.</param>
	/// <param name="keyToString">Extract key from item.</param>
	/// <param name="settings">Settings.</param>
	/// <param name="logger">Logger.</param>
	/// <param name="time">Time source.</param>
	/// <typeparam name="T">Type of item.</typeparam>
	/// <returns>New instance of <see cref="IAliveKeeper{T}"/></returns>
	public static IAliveKeeper<T> Create<T>(
		Func<T[], Task<T[]>> touchAction,
		Func<T[], Task<T[]>>? deleteAction,
		Func<T, string>? keyToString = null,
		IAliveKeeperSettings? settings = null,
		ILogger? logger = null,
		ITimeSource? time = null)
		where T: notnull =>
		new AliveKeeper<T>(
			touchAction, deleteAction, keyToString,
			settings, logger, time);
}