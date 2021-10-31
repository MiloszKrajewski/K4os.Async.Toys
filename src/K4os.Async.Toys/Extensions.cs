using System.Collections.Generic;
using System.Threading.Tasks;
using K4os.Async.Toys.Internal;

namespace K4os.Async.Toys
{
	public static class Extensions
	{
		public static void EnqueueMany<T>(
			this IAgent<T> agent, IEnumerable<T> sequence) =>
			sequence.ForEach(agent.Enqueue);

		public static Task<T> Dequeue<T>(
			this IAgentContext<T> agent) =>
			agent.Queue.ReadAsync(agent.Token).AsTask();

		public static Task<List<T>?> DequeueMany<T>(
			this IAgentContext<T> agent, int length = int.MaxValue) =>
			agent.Queue.ReadManyAsync(length, agent.Token);
	}
}
