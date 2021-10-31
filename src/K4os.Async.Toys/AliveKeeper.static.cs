using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace K4os.Async.Toys
{
	public static class AliveKeeper
	{
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
}
