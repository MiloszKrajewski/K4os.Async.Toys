using System;
using Microsoft.Extensions.Logging;

namespace K4os.Async.Toys;

public partial class Agent
{
	/// <summary>Creates and starts new agent.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>New agent.</returns>
	public static Agent Create(
		Func<IAgentContext, Task> action, 
		ILogger? logger = null,
		CancellationToken token = default) =>
		new(Forever(action), logger, token);

	/// <summary>Creates and starts new agent.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>New agent.</returns>
	public static Agent Create(
		Func<IAgentContext, Task<bool>> action, 
		ILogger? logger = null,
		CancellationToken token = default) =>
		new(action, logger, token);

	/// <summary>Creates and starts new agent with inbox queue.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>New agent.</returns>
	public static Agent<T> Create<T>(
		Func<IAgentContext<T>, Task> action, 
		ILogger? logger = null,
		CancellationToken token = default) =>
		new(Forever(action), logger, token);

	/// <summary>Creates and starts new agent with inbox queue.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>New agent.</returns>
	public static Agent<T> Create<T>(
		Func<IAgentContext<T>, Task<bool>> action, 
		ILogger? logger = null,
		CancellationToken token = default) =>
		new(action, logger, token);

	/// <summary>Creates and starts new agent.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>New agent.</returns>
	public static Agent Launch(
		Func<IAgentContext, Task> action, 
		ILogger? logger = null,
		CancellationToken token = default)
	{
		var agent = Create(action, logger, token);
		agent.Start();
		return agent;
	}

	/// <summary>Creates and starts new agent.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>New agent.</returns>
	public static Agent Launch(
		Func<IAgentContext, Task<bool>> action, 
		ILogger? logger = null,
		CancellationToken token = default)
	{
		var agent = Create(action, logger, token);
		agent.Start();
		return agent;
	}

	/// <summary>Creates and starts new agent with inbox queue.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>New agent.</returns>
	public static Agent<T> Launch<T>(
		Func<IAgentContext<T>, Task> action, 
		ILogger? logger = null,
		CancellationToken token = default)
	{
		var agent = Create(action, logger, token);
		agent.Start();
		return agent;
	}

	/// <summary>Creates and starts new agent with inbox queue.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>New agent.</returns>
	public static Agent<T> Launch<T>(
		Func<IAgentContext<T>, Task<bool>> action, 
		ILogger? logger = null,
		CancellationToken token = default)
	{
		var agent = Create(action, logger, token);
		agent.Start();
		return agent;
	}
	
	private static Func<T, Task<bool>> Forever<T>(Func<T, Task> action) =>
		async ctx => {
			await action(ctx).ConfigureAwait(false);
			return true;
		};
}
