using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace K4os.Async.Toys
{
	public partial class Agent
	{
		/// <summary>Creates and starts new agent.</summary>
		/// <param name="action">Action to be executed (continuously) by agent.</param>
		/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
		/// <returns>New agent.</returns>
		public Agent Create(Func<IAgentContext, Task> action, ILogger? logger = null)
		{
			var agent = new Agent(action, logger);
			agent.Start();
			return agent;
		}

		/// <summary>Creates and starts new agent with inbox queue.</summary>
		/// <param name="action">Action to be executed (continuously) by agent.</param>
		/// <param name="logger">Logger to be used (can be <c>null</c>)</param>
		/// <returns>New agent.</returns>
		public Agent<T> Create<T>(Func<IAgentContext<T>, Task> action, ILogger? logger = null)
		{
			var agent = new Agent<T>(action, logger);
			agent.Start();
			return agent;
		}
	}
}
