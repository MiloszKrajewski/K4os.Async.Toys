using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace K4os.Async.Toys
{
	/// <summary>Batch builder factory.</summary>
	public static class BatchBuilder
	{
		internal static readonly UnboundedChannelOptions ChannelOptions = new() {
			SingleReader = true,
		};

		private static T Pass<T>(T x) => x;

		public static IBatchBuilder<TKey, TKey> Create<TKey>(
			Func<TKey[], Task<TKey[]>> requestMany,
			IBatchBuilderSettings? settings = null,
			ILogger? logger = null,
			ITimeSource? time = null)
			where TKey: notnull =>
			new BatchBuilder<TKey, TKey, TKey>(
				Pass, Pass, requestMany, settings, logger, time);

		public static IBatchBuilder<TRequest, TResponse> Create<TRequest, TResponse>(
			Func<TResponse, TRequest> responseKey,
			Func<TRequest[], Task<TResponse[]>> requestMany,
			IBatchBuilderSettings? settings = null,
			ILogger? logger = null,
			ITimeSource? time = null)
			where TRequest: notnull =>
			new BatchBuilder<TRequest, TRequest, TResponse>(
				Pass, responseKey, requestMany, settings, logger, time);

		public static IBatchBuilder<TRequest, TResponse> Create<TKey, TRequest, TResponse>(
			Func<TRequest, TKey> requestKey,
			Func<TResponse, TKey> responseKey,
			Func<TRequest[], Task<TResponse[]>> requestMany,
			IBatchBuilderSettings? settings = null,
			ILogger? logger = null,
			ITimeSource? time = null)
			where TKey: notnull =>
			new BatchBuilder<TKey, TRequest, TResponse>(
				requestKey, responseKey, requestMany, settings, logger, time);
	}
}
