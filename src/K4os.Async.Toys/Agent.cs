using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Async.Toys
{
	public abstract class Agent: IDisposable
	{
		protected static readonly UnboundedChannelOptions ChannelOptions = new() {
			SingleReader = true,
			AllowSynchronousContinuations = false,
		};

		protected readonly ILogger Log;

		private readonly CancellationTokenSource _cancel;
		private readonly Task _task;

		protected Agent(ILogger? logger)
		{
			Log = logger ?? NullLogger.Instance;
			_cancel = new CancellationTokenSource();
			_task = Task.Run(() => Loop(_cancel.Token));
		}

		public CancellationToken Token => _cancel.Token;
		public bool IsActive => !_cancel.IsCancellationRequested;

		private async Task Loop(CancellationToken token)
		{
			while (true)
			{
				try
				{
					token.ThrowIfCancellationRequested();
					await Execute(token);
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

		protected abstract Task Execute(CancellationToken token);

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			_cancel.Cancel();
			_task.Wait(CancellationToken.None);

			_cancel.Dispose();
			_task.Dispose();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}

	public abstract class Agent<T>: Agent
	{
		private readonly Channel<T> _queue =
			Channel.CreateUnbounded<T>(ChannelOptions);

		protected Agent(ILogger? logger): base(logger) { }

		public void Enqueue(T item)
		{
			if (_queue.Writer.TryWrite(item))
				return;

			throw QueueIsFull();
		}

		protected Task<T> Dequeue(CancellationToken token) =>
			_queue.Reader.ReadAsync(token).AsTask();

		protected async Task<IEnumerable<T>> DequeueMany(CancellationToken token)
		{
			await _queue.Reader.WaitToReadAsync(token);
			return Drain(_queue.Reader);
		}

		private static IEnumerable<T> Drain(ChannelReader<T> queueReader)
		{
			while (queueReader.TryRead(out var item))
				yield return item;
		}

		private static InvalidOperationException QueueIsFull() =>
			new("Internal queue is full");
	}
}
