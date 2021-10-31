using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using K4os.Async.Toys.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Async.Toys
{
	public interface IAgent: IDisposable
	{
		void Start();
		public Task Done { get; }
	}
	
	public interface IAgent<in T>: IAgent
	{
		public void Enqueue(T item);
	}

	public interface IAgentContext
	{
		ILogger Log { get; }
		CancellationToken Token { get; }
	}

	public interface IAgentContext<T>: IAgentContext
	{
		ChannelReader<T> Queue { get; }
	}

	/// <summary>Agent base class.</summary>
	public abstract class AbstractAgent: IAgent, IAgentContext
	{
		/// <summary>Default agent channel options.</summary>
		protected static readonly UnboundedChannelOptions ChannelOptions = new() {
			SingleReader = true,
			AllowSynchronousContinuations = false,
		};
		
		protected ILogger Log { get; }

		private readonly CancellationTokenSource _cancel;

		private readonly TaskCompletionSource<object?> _ready =
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		private readonly Task _done;

		/// <summary>Creates new agent.</summary>
		/// <param name="logger">Logger.</param>
		protected AbstractAgent(ILogger? logger)
		{
			Log = logger ?? NullLogger.Instance;
			_cancel = new CancellationTokenSource();
			_done = Task.Run(Loop);
		}

		/// <summary>Starts agent. If agent is already running does nothing.</summary>
		public void Start() => _ready.TrySetResult(null);

		/// <summary>Agent stopped processing..</summary>
		public Task Done => _done;

		private async Task Loop()
		{
			await _ready.Task.ConfigureAwait(false);

			var token = _cancel.Token;

			while (true)
			{
				try
				{
					token.ThrowIfCancellationRequested();
					await Execute().ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (token.IsCancellationRequested)
				{
					Log.LogDebug("Execution canceled");
					return;
				}
				catch (Exception e)
				{
					Log.LogError(e, "Background task execution failed");
				}
			}
		}

		/// <summary>Actual implementation of single iteration.</summary>
		/// <returns>Task indicating iteration is finished.</returns>
		protected abstract Task Execute();

		/// <summary>Stops agent.</summary>
		/// <param name="disposing"><c>true</c> if agents is disposed by user.</param>
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			_cancel.Cancel();
			_ready.TrySetCanceled(_cancel.Token);
			_done.Wait(CancellationToken.None);

			_cancel.Dispose();
			_done.Dispose();
		}

		/// <summary>Stop and dispose agent.</summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>Log.</summary>
		ILogger IAgentContext.Log => Log;

		/// <summary>Cancellation token.</summary>
		CancellationToken IAgentContext.Token => _cancel.Token;
	}

	/// <summary>Agent base class.</summary>
	public partial class Agent: AbstractAgent
	{
		private readonly Func<IAgentContext, Task> _action;

		/// <summary>Creates new agent.</summary>
		/// <param name="logger">Logger.</param>
		/// <param name="action">Action to be executed in the loop.</param>
		public Agent(Func<IAgentContext, Task> action, ILogger? logger = null): base(logger) =>
			_action = action.Required(nameof(action));

		protected override Task Execute() => _action(this);
	}

	/// <summary>Agent with a queue base class.</summary>
	/// <typeparam name="T">Type of items.</typeparam>
	public class Agent<T>: AbstractAgent, IAgent<T>, IAgentContext<T>
	{
		private readonly Channel<T> _queue = Channel.CreateUnbounded<T>(ChannelOptions);
		private readonly Func<IAgentContext<T>, Task> _action;

		/// <summary>Creates new agent and starts it.</summary>
		/// <param name="logger">Log.</param>
		/// <param name="action">Agent's action.</param>
		public Agent(Func<IAgentContext<T>, Task> action, ILogger? logger = null): base(logger) => 
			_action = action.Required(nameof(action));

		protected override Task Execute() => _action(this);

		/// <summary>Enqueues item to be processed by agent.</summary>
		/// <param name="item">Item.</param>
		/// <exception cref="InvalidOperationException">Thrown when queue is full.</exception>
		public void Enqueue(T item)
		{
			if (_queue.Writer.TryWrite(item))
				return;

			throw QueueIsFull();
		}

		private static InvalidOperationException QueueIsFull() =>
			new("Internal queue is full");
		
		ChannelReader<T> IAgentContext<T>.Queue => _queue;
	}
}
