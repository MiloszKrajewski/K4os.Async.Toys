using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace K4os.Async.Toys
{
	public partial class Agent
	{
		public Agent Create(Func<IAgentContext, Task> action, ILogger? logger = null)
		{
			var agent = new Agent(action, logger);
			agent.Start();
			return agent;
		}

		public Agent<T> Create<T>(Func<IAgentContext<T>, Task> action, ILogger? logger = null)
		{
			var agent = new Agent<T>(action, logger);
			agent.Start();
			return agent;
		}
	}
}
