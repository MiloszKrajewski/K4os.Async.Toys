using System;
using Microsoft.Extensions.Logging;

namespace K4os.Async.Toys;

public partial class Agent
{
	/// <summary>Creates and starts new agent.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <returns>New agent.</returns>
	public static Agent Create(Func<IAgentContext, Task> action, ILogger? logger = null) =>
		new(Endless(action), logger);

	/// <summary>Creates and starts new agent.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <returns>New agent.</returns>
	public static Agent Create(Func<IAgentContext, Task<bool>> action, ILogger? logger = null) =>
		new(action, logger);

	/// <summary>Creates and starts new agent with inbox queue.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <returns>New agent.</returns>
	public static Agent<T> Create<T>(
		Func<IAgentContext<T>, Task> action, ILogger? logger = null) =>
		new(Endless(action), logger);
	
	/// <summary>Creates and starts new agent with inbox queue.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <returns>New agent.</returns>
	public static Agent<T> Create<T>(
		Func<IAgentContext<T>, Task<bool>> action, ILogger? logger = null) =>
		new(action, logger);


	/// <summary>Creates and starts new agent.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <returns>New agent.</returns>
	public static Agent Launch(Func<IAgentContext, Task> action, ILogger? logger = null)
	{
		var agent = Create(action, logger);
		agent.Start();
		return agent;
	}
	
	/// <summary>Creates and starts new agent.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <returns>New agent.</returns>
	public static Agent Launch(Func<IAgentContext, Task<bool>> action, ILogger? logger = null)
	{
		var agent = Create(action, logger);
		agent.Start();
		return agent;
	}

	/// <summary>Creates and starts new agent with inbox queue.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <returns>New agent.</returns>
	public static Agent<T> Launch<T>(
		Func<IAgentContext<T>, Task> action, ILogger? logger = null)
	{
		var agent = Create(action, logger);
		agent.Start();
		return agent;
	}
	
	/// <summary>Creates and starts new agent with inbox queue.</summary>
	/// <param name="action">Action to be executed (continuously) by agent.</param>
	/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
	/// <returns>New agent.</returns>
	public static Agent<T> Launch<T>(
		Func<IAgentContext<T>, Task<bool>> action, ILogger? logger = null)
	{
		var agent = Create(action, logger);
		agent.Start();
		return agent;
	}
	
	private static Func<T, Task<bool>> Endless<T>(Func<T, Task> action) =>
		async ctx => {
			await action(ctx).ConfigureAwait(false);
			return true;
		};
}
