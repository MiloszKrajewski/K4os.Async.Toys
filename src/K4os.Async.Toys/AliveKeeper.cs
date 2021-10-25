using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using K4os.Async.Toys.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Async.Toys
{
	public class AliveKeeper<T>: IDisposable
		where T: notnull
	{
		protected readonly ILogger Log;

		private readonly ITimeSource _time;
		private readonly IAliveKeeperSettings _settings;

		private readonly ConcurrentDictionary<T, int> _items = new();

		private readonly Func<T[], Task<T[]>> _touchAction;
		private readonly Func<T[], Task<T[]>>? _deleteAction;
		private readonly IBatchBuilder<T, T> _touchBatch;
		private readonly IBatchBuilder<T, T> _deleteBatch;

		private readonly SemaphoreSlim _mutex = new(1);

		protected AliveKeeper(
			Func<T[], Task<T[]>> touchAction,
			Func<T[], Task<T[]>>? deleteAction,
			IAliveKeeperSettings? settings = null,
			ILogger? log = null,
			ITimeSource? time = null)
		{
			Log = log ?? NullLogger.Instance;
			_time = time ?? TimeSource.Default;
			_settings = settings = Validate(settings ?? new AliveKeeperSettings());

			_touchAction = touchAction.Required(nameof(touchAction));
			_deleteAction = deleteAction;

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

		private async Task TouchOne(T item)
		{
			try
			{
				await _touchBatch.Request(item);
			}
			catch (Exception) when (!IsActive(item))
			{
				// this can happen but we don't care anymore 
			}
		}

		private async Task DeleteOne(T item)
		{
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
			key.ToString() ?? "<null>";

		private async Task TouchOneLoop(T item, CancellationToken token)
		{
			var interval = _settings.TouchInterval;
			var retry = _settings.RetryInterval;
			var retryLimit = _settings.RetryLimit;

			var display = Display(item);
			var failed = 0;
			while (!token.IsCancellationRequested)
			{
				await Delay(failed > 0 ? retry : interval, token);

				if (!IsActive(item)) return;

				try
				{
					Log.LogDebug("Touching [{0}]...", display);
					await TouchOne(item);
					failed = 0;
				}
				catch (Exception e)
				{
					if (failed++ > retryLimit)
					{
						Log.LogError(
							e,
							"Touching [{0}] failed {1} time(s), giving up!",
							display, failed);
						Deactivate(item);
						return;
					}

					Log.LogWarning(
						e,
						"Touching [{0}] failed {1} time(s), retrying...",
						display, failed);
					Deactivate(item);
				}
			}
		}

		protected async Task DeleteOneLoop(T item, CancellationToken token)
		{
			var retry = _settings.RetryInterval;
			var retryLimit = _settings.RetryLimit;

			var display = Display(item);
			var failed = 0;
			while (!token.IsCancellationRequested)
			{
				if (failed > 0) await Delay(retry, token);
				if (!IsActive(item)) return;

				try
				{
					Log.LogDebug("Deleting [{0}]...", display);
					await DeleteOne(item);
					return;
				}
				catch (Exception e)
				{
					if (failed++ > retryLimit)
					{
						Log.LogError(
							e,
							"Touching [{0}] failed {1} time(s), giving up!",
							display, failed);
						Deactivate(item);
						return;
					}

					Log.LogWarning(
						e,
						"Deleting [{0}] failed {1} time(s), retrying...",
						display, failed);
					Deactivate(item);
				}
			}
		}

		public void Upkeep(T item, CancellationToken token = default)
		{
			if (!TryActivate(item))
				return;

			Task.Run(() => TouchOneLoop(item, token), token).Forget();
		}

		public async Task Delete(T item, CancellationToken token = default)
		{
			if (!IsActive(item))
				return;

			await DeleteOneLoop(item, token);

			Deactivate(item);
		}

		public void Forget(T item) => Deactivate(item);

		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

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
