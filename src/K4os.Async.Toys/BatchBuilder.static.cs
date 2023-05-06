using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace K4os.Async.Toys;

/// <summary>Batch builder factory.</summary>
public static class BatchBuilder
{
	internal static readonly UnboundedChannelOptions ChannelOptions = new() {
		SingleReader = true,
	};

	private static T Pass<T>(T x) => x;

	/// <summary>Creates batch builder.</summary>
	/// <param name="requestMany">Callback to send multiple requests.</param>
	/// <param name="settings">Settings.</param>
	/// <param name="logger">Logger.</param>
	/// <param name="time">Time source.</param>
	/// <typeparam name="TKey">Type of request.</typeparam>
	/// <returns>New batch builder.</returns>
	public static IBatchBuilder<TKey, TKey> Create<TKey>(
		Func<TKey[], Task<TKey[]>> requestMany,
		IBatchBuilderSettings? settings = null,
		ILogger? logger = null,
		ITimeSource? time = null)
		where TKey: notnull =>
		new BatchBuilder<TKey, TKey, TKey>(
			Pass, Pass, requestMany, settings, logger, time);

	/// <summary>Creates batch builder. Used when Request is it's own key.</summary>
	/// <param name="responseKey">Extract request key from response.</param>
	/// <param name="requestMany">Callback to send multiple requests.</param>
	/// <param name="settings">Settings.</param>
	/// <param name="logger">Logger.</param>
	/// <param name="time">Time source.</param>
	/// <typeparam name="TRequest">Type of request.</typeparam>
	/// <typeparam name="TResponse">Type of response.</typeparam>
	/// <returns>New batch builder.</returns>
	public static IBatchBuilder<TRequest, TResponse> Create<TRequest, TResponse>(
		Func<TResponse, TRequest> responseKey,
		Func<TRequest[], Task<TResponse[]>> requestMany,
		IBatchBuilderSettings? settings = null,
		ILogger? logger = null,
		ITimeSource? time = null)
		where TRequest: notnull =>
		new BatchBuilder<TRequest, TRequest, TResponse>(
			Pass, responseKey, requestMany, settings, logger, time);

	/// <summary>Creates batch builder.</summary>
	/// <param name="requestKey">Extract key from request.</param>
	/// <param name="responseKey">Extract key from response.</param>
	/// <param name="requestMany">Callback to send multiple requests.</param>
	/// <param name="settings">Settings.</param>
	/// <param name="logger">Logger.</param>
	/// <param name="time">Time source.</param>
	/// <typeparam name="TKey">Type of request/response correlation key.</typeparam>
	/// <typeparam name="TRequest">Type of request.</typeparam>
	/// <typeparam name="TResponse">Type of response.</typeparam>
	/// <returns>New batch builder.</returns>
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