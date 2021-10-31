using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using K4os.Async.Toys.Internal;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Async.Toys
{
	public interface IAliveKeeper<in T>: IDisposable
	{
		void Upkeep(T item, CancellationToken token = default);
		Task Delete(T item, CancellationToken token = default);
		void Forget(T item);

		Task Shutdown(CancellationToken token = default);
	}

	public class AliveKeeper<T>: IAliveKeeper<T> where T: notnull
	{
		protected readonly ILogger Log;

		private readonly ITimeSource _time;
		private readonly IAliveKeeperSettings _settings;

		private readonly ConcurrentDictionary<T, int> _items = new();

		private readonly Func<T[], Task<T[]>> _touchAction;
		private readonly Func<T[], Task<T[]>>? _deleteAction;
		private readonly Func<T, string>? _keyToString;

		private readonly IBatchBuilder<T, T> _touchBatch;
		private readonly IBatchBuilder<T, T> _deleteBatch;

		private readonly CancellationTokenSource _cancel = new();

		private readonly SemaphoreSlim _mutex = new(1);

		public AliveKeeper(
			Func<T[], Task<T[]>> touchAction,
			Func<T[], Task<T[]>>? deleteAction,
			Func<T, string>? keyToString = null,
			IAliveKeeperSettings? settings = null,
			ILogger? log = null,
			ITimeSource? time = null)
		{
			Log = log ?? NullLogger.Instance;
			_time = time ?? TimeSource.Default;
			_settings = settings = Validate(settings ?? new AliveKeeperSettings());

			_touchAction = touchAction.Required(nameof(touchAction));
			_deleteAction = deleteAction;
			_keyToString = keyToString;

			_touchBatch = new BatchBuilder<T, T, T>(
				Pass, Pass, TouchMany,
				new BatchBuilderSettings {
					BatchSize = settings.TouchBatchSize,
					BatchDelay = settings.TouchBatchDelay,
					Concurrency = 1,
				},
				log, time);

			_deleteBatch = new BatchBuilder<T, T, T>(
				Pass, Pass, DeleteMany,
				new BatchBuilderSettings {
					BatchSize = settings.DeleteBatchSize,
					BatchDelay = TimeSpan.Zero,
					Concurrency = 1,
				},
				log, time);
		}

		private static IAliveKeeperSettings Validate(IAliveKeeperSettings settings) =>
			new AliveKeeperSettings {
				TouchInterval = settings.TouchInterval.NotLessThan(TimeSpan.Zero),
				TouchBatchSize = settings.TouchBatchSize.NotLessThan(1),
				TouchBatchDelay = settings.TouchBatchDelay.NotLessThan(TimeSpan.Zero),
				DeleteBatchSize = settings.DeleteBatchSize.NotLessThan(1),
				RetryInterval = settings.RetryInterval.NotLessThan(TimeSpan.Zero),
				RetryLimit = settings.RetryLimit.NotLessThan(0),
			};

		private static T Pass(T x) => x;
		private bool IsActive(T item) => _items.ContainsKey(item);
		private bool IsDisposing => _cancel.IsCancellationRequested;
		private T[] ActiveOnly(IEnumerable<T> items) => items.Where(IsActive).ToArray();
		private void Deactivate(T item) => _items.TryRemove(item, out _);
		private bool TryActivate(T item) => _items.TryAdd(item, 0);

		protected Task<T[]> OnTouch(T[] items) =>
			_touchAction(items);

		protected Task<T[]> OnDelete(T[] items) =>
			_deleteAction is null ? Task.FromResult(items) : _deleteAction(items);

		protected virtual Task Delay(TimeSpan delay, CancellationToken token) =>
			_time.Delay(delay, token);

		private async Task<T[]> TouchMany(T[] items)
		{
			await _mutex.WaitAsync();
			try
			{
				return await OnTouch(ActiveOnly(items));
			}
			finally
			{
				_mutex.Release();
			}
		}

		private async Task<T[]> DeleteMany(T[] items)
		{
			await _mutex.WaitAsync();
			try
			{
				return await OnDelete(ActiveOnly(items));
			}
			finally
			{
				_mutex.Release();
			}
		}

		private async Task TouchOne(string display, T item)
		{
			Log.LogDebug("Touching [{0}]...", display);

			try
			{
				await _touchBatch.Request(item);
			}
			catch (Exception) when (!IsActive(item))
			{
				// this can happen but we don't care anymore 
			}
		}

		private async Task DeleteOne(string display, T item)
		{
			Log.LogDebug("Deleting [{0}]...", display);

			try
			{
				await _deleteBatch.Request(item);
			}
			catch (Exception) when (!IsActive(item))
			{
				// this can happen but we don't care anymore 
			}
		}

		protected virtual string Display(T key) =>
			_keyToString?.Invoke(key) ?? key.ToString() ?? "<null>";

		private async Task TouchOneLoop(T item, CancellationToken token)
		{
			if (!TryActivate(item))
				return;

			try
			{
				token = CancellationTokenSource.CreateLinkedTokenSource(token, _cancel.Token).Token;

				var interval = _settings.TouchInterval;
				var retry = _settings.RetryInterval;

				var display = Display(item);
				var failed = 0;
				while (!token.IsCancellationRequested)
				{
					await Delay(failed > 0 ? retry : interval, token);

					if (!IsActive(item)) return;

					try
					{
						await TouchOne(display, item);
						failed = 0;
					}
					catch (Exception e)
					{
						if (!OnOperationFailed(e, "Touch", display, ++failed))
							return;
					}
				}
			}
			finally
			{
				Deactivate(item);
			}
		}

		protected async Task DeleteOneLoop(T item, CancellationToken token)
		{
			if (!IsActive(item))
				return;

			try
			{
				var retry = _settings.RetryInterval;

				var display = Display(item);
				var failed = 0;
				while (!token.IsCancellationRequested)
				{
					if (failed > 0)
					{
						if (_cancel.IsCancellationRequested)
							return; // don't retry if disposing

						await Delay(retry, token);
					}

					if (!IsActive(item)) 
						return;

					try
					{
						await DeleteOne(display, item);
						return;
					}
					catch (Exception e)
					{
						if (!OnOperationFailed(e, "Delete", display, ++failed))
							return;
					}
				}
			}
			finally
			{
				Deactivate(item);
			}
		}

		private bool OnOperationFailed(
			Exception exception, string operation, string display, int failed)
		{
			var retryLimit = _settings.RetryLimit;
			var giveUp = failed > retryLimit;
			var level = giveUp ? LogLevel.Error : LogLevel.Warning;
			var outcome = giveUp ? "giving up!" : "retrying...";

			Log.Log(
				level,
				exception,
				"Operation {0}({1}) failed {2} time(s), {3}",
				operation, display, failed, outcome);

			return !giveUp;
		}

		public void Upkeep(T item, CancellationToken token = default)
		{
			if (IsDisposing) 
				return;

			Task.Run(() => TouchOneLoop(item, token), token).Forget();
		}

		public async Task Delete(T item, CancellationToken token = default)
		{
			if (IsDisposing)
				return;

			await DeleteOneLoop(item, token);
		}

		public void Forget(T item) => Deactivate(item);

		public async Task Shutdown(CancellationToken token = default)
		{
			var delay = 0.01;
			_cancel.Cancel();

			while (true)
			{
				if (_items.IsEmpty) break;

				await Delay(TimeSpan.FromSeconds(delay), token);
				delay = Math.Min(1, delay * 1.5);
			}
		}

		public void ShutdownAndWait(CancellationToken token = default)
		{
			var task = Task.Factory.StartNew(
				() => Shutdown(token),
				TaskCreationOptions.LongRunning);
			task.Wait(token);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			ShutdownAndWait();

			DisposableBag.DisposeMany(
				_deleteBatch,
				_touchBatch,
				_mutex);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
