using K4os.Async.Toys.Internal;

namespace K4os.Async.Toys;

/// <summary>
/// Extensions for agents.
/// </summary>
public static class AgentExtensions
{
	/// <summary>Enqueues many messages for agent.</summary>
	/// <param name="agent">Agent.</param>
	/// <param name="sequence">Sequence of messages.</param>
	/// <typeparam name="T">Type of message.</typeparam>
	public static void EnqueueMany<T>(
		this IAgent<T> agent, IEnumerable<T> sequence) =>
		sequence.ForEach(agent.Enqueue);

	/// <summary>
	/// Dequeues one message from agent.
	/// </summary>
	/// <param name="agent">Agent.</param>
	/// <typeparam name="T">Type of message.</typeparam>
	/// <returns>One message.</returns>
	public static Task<T> Dequeue<T>(
		this IAgentContext<T> agent) =>
		agent.Queue.Reader.ReadAsync(agent.Token).AsTask();

	/// <summary>
	/// Dequeues many messages from agent.
	/// </summary>
	/// <param name="agent">Agent.</param>
	/// <param name="length">Maximum number of messages.</param>
	/// <typeparam name="T">Type of message.</typeparam>
	/// <returns>Multiple messages.</returns>
	public static Task<List<T>?> DequeueMany<T>(
		this IAgentContext<T> agent, int length = int.MaxValue) =>
		agent.Queue.Reader.ReadManyAsync(length, agent.Token);
}