using System;
using System.Collections.Concurrent;
using K4os.Async.Toys.Internal;
using System.Linq;
using K4os.Async.Toys.SyncPolicy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace K4os.Async.Toys;

/// <summary>Component maintaining claims over items alive in predefined intervals.</summary>
/// <typeparam name="T">Type of item.</typeparam>
public interface IAliveKeeper<in T>: IDisposable
{
	/// <summary>Start keeping claim alive.</summary>
	/// <param name="item">Claim to be kept alive.</param>
	/// <param name="token">Cancellation token.</param>
	void Upkeep(T item, CancellationToken token = default);

	/// <summary>
	/// Deletes claim. Please note, this method not only stops keeping claim alive, but
	/// also actively executes delete action on it indicating that item is no longer needed
	/// (ie: has been successfully processed).
	/// </summary>
	/// <param name="item">Claim to be deleted.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>Task indicating that operation has been successful.</returns>
	Task Delete(T item, CancellationToken token = default);

	/// <summary>
	/// Forgets claim. Please note, this method just stops keeping the claim, so other
	/// can take it, for example when item has not been processed, but processor is
	/// giving up. 
	/// </summary>
	/// <param name="item">Claim to be forgotten.</param>
	void Forget(T item);

	/// <summary>
	/// Shuts keeping claim alive down. All actions which are in progress are going to be
	/// finished but all "keep alive loops" are going to be cancelled (<see cref="Forget"/>).
	/// </summary>
	/// <param name="token">Cancellation token.</param>
	/// <returns>Task indicating that operation has been successful.</returns>
	Task Shutdown(CancellationToken token = default);
}

/// <summary>Component maintaining claims over items alive in predefined intervals.</summary>
/// <typeparam name="T">Type of item.</typeparam>
public class AliveKeeper<T>: IAliveKeeper<T> where T: notnull
{
	/// <summary>Log.</summary>
	protected readonly ILogger Log;

	private readonly ITimeSource _time;
	private readonly IAliveKeeperSettings _settings;

	private readonly ConcurrentDictionary<T, object?> _items = new();

	private readonly Func<T[], Task<T[]>> _touchAction;
	private readonly Func<T[], Task<T[]>>? _deleteAction;
	private readonly Func<T, string>? _keyToString;

	private readonly IBatchBuilder<T, T> _touchBatch;
	private readonly IBatchBuilder<T, T> _deleteBatch;
	private readonly IAliveKeeperSyncPolicy _syncPolicy;

	private readonly CancellationTokenSource _cancel = new();

	/// <summary>
	/// Creates new instance of <see cref="AliveKeeper{T}"/>.
	/// </summary>
	/// <param name="touchAction">Action performed to keep object claim.</param>
	/// <param name="deleteAction">Action performed to delete object.</param>
	/// <param name="keyToString">Extracts key from object.</param>
	/// <param name="settings">Settings.</param>
	/// <param name="log">Logger.</param>
	/// <param name="time">Time source.</param>
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
		
		_syncPolicy = settings.SyncPolicy switch {
			AliveKeeperSyncPolicy.Safe => new SafeSyncPolicy(),
			_ when _settings.Concurrency <= 1 => new SafeSyncPolicy(),
			AliveKeeperSyncPolicy.Unrestricted => UnrestrictedSyncPolicy.Instance,
			AliveKeeperSyncPolicy.Alternating => new AlternatingSyncPolicy(),
			_ => new SafeSyncPolicy(),
		};

		_touchBatch = new BatchBuilder<T, T, T>(
			Pass, Pass, TouchMany,
			new BatchBuilderSettings {
				BatchSize = settings.TouchBatchSize,
				BatchDelay = settings.TouchBatchDelay,
				Concurrency = settings.Concurrency,
			},
			log, time);

		_deleteBatch = new BatchBuilder<T, T, T>(
			Pass, Pass, DeleteMany,
			new BatchBuilderSettings {
				BatchSize = settings.DeleteBatchSize,
				BatchDelay = TimeSpan.Zero,
				Concurrency = settings.Concurrency,
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
			Concurrency = settings.Concurrency.NotLessThan(1),
			SyncPolicy = settings.SyncPolicy,
		};

	private static T Pass(T x) => x;
	private bool IsActive(T item) => _items.ContainsKey(item);
	private bool IsDisposing => _cancel.IsCancellationRequested;

	private T[] ActiveOnly(T[] items)
	{
		// this method is optimized for the fact that most of the time
		// all of them will be active, so same array can be returned
		return items.Length <= 0 || items.All(IsActive) 
			? items
			: items.Where(IsActive).ToArray();
	}
	
	private void Deactivate(T item) => _items.TryRemove(item, out _);
	private bool TryActivate(T item) => _items.TryAdd(item, null);

	private static Task<T[]> EmptyArrayOfT() => Task.FromResult(Array.Empty<T>());

	/// <summary>Action to touch / keep claim on objects.</summary>
	/// <param name="items">Objects to update.</param>
	/// <returns>Successfully touched items.</returns>
	protected Task<T[]> OnTouch(T[] items) =>
		items.Length <= 0 ? EmptyArrayOfT() : _touchAction(items);

	/// <summary>Action to delete objects.</summary>
	/// <param name="items">Objects to delete.</param>
	/// <returns>Successfully deleted items.</returns>
	protected Task<T[]> OnDelete(T[] items) =>
		items.Length <= 0 ? EmptyArrayOfT() :
		_deleteAction is null ? Task.FromResult(items) :
		_deleteAction(items);

	/// <summary>Delays execution.</summary>
	/// <param name="delay">Delay.</param>
	/// <param name="token">Cancellation token.</param>
	/// <returns>Task.</returns>
	protected virtual Task Delay(TimeSpan delay, CancellationToken token) =>
		_time.Delay(delay, token);

	private async Task<T[]> TouchMany(T[] items)
	{
		await _syncPolicy.EnterTouch();
		try
		{
			return await OnTouch(ActiveOnly(items));
		}
		finally
		{
			_syncPolicy.LeaveTouch();
		}
	}

	private async Task<T[]> DeleteMany(T[] items)
	{
		await _syncPolicy.EnterDelete();
		try
		{
			return await OnDelete(ActiveOnly(items));
		}
		finally
		{
			_syncPolicy.LeaveDelete();
		}
	}

	private async Task TouchOne(string display, T item)
	{
		Log.LogDebug("Touching [{Key}]...", display);

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
		Log.LogDebug("Deleting [{Key}]...", display);

		try
		{
			await _deleteBatch.Request(item);
		}
		catch (Exception) when (!IsActive(item))
		{
			// this can happen but we don't care anymore 
		}
	}

	/// <summary>Method to display item key.</summary>
	/// <param name="key">Key.</param>
	/// <returns>Key in human readable format.</returns>
	protected virtual string Display(T key) =>
		// ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
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

	/// <summary>Called when item needs to be deleted.</summary>
	/// <param name="item">Item to be deleted.</param>
	/// <param name="token">Cancellation token.</param>
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
			"Operation {Operation}({Item}) failed {Count} time(s), {Outcome}",
			operation, display, failed, outcome);

		return !giveUp;
	}

	/// <summary>
	/// Starts a loop to touch / keep claim alive.
	/// </summary>
	/// <param name="item">Item.</param>
	/// <param name="token">Cancellation token.</param>
	public void Upkeep(T item, CancellationToken token = default)
	{
		if (IsDisposing)
			return;

		Task.Run(() => TouchOneLoop(item, token), token).Forget();
	}

	/// <summary>
	/// Deletes item when it hase been processed.
	/// </summary>
	/// <param name="item">Item.</param>
	/// <param name="token">Cancellation token.</param>
	public async Task Delete(T item, CancellationToken token = default)
	{
		if (IsDisposing)
			return;

		await DeleteOneLoop(item, token);
	}

	/// <summary>
	/// Forgets about item. It does not mean it gets deleted, it just stops keeping it alive.
	/// </summary>
	/// <param name="item">Item.</param>
	public void Forget(T item) => Deactivate(item);

	/// <summary>
	/// Shuts down all keep alive loops.
	/// </summary>
	/// <param name="token">Cancellation token.</param>
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

	/// <summary>
	/// Shuts down all keep alive loops, and waits for them to finish.
	/// </summary>
	/// <param name="token">Cancellation token.</param>
	public void ShutdownAndWait(CancellationToken token = default)
	{
		var task = Task.Factory.StartNew(
			() => Shutdown(token),
			TaskCreationOptions.LongRunning);
		task.Wait(token);
	}

	/// <summary>
	/// Disposes the <see cref="AliveKeeper{T}"/>.
	/// Shuts down all keep alive loops and waits for them to finish.
	/// </summary>
	/// <param name="disposing"><c>true</c> if triggered by user.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (!disposing) return;

		ShutdownAndWait();

		DisposableBag.DisposeMany(
			_deleteBatch,
			_touchBatch,
			_syncPolicy);
	}

	/// <summary>
	/// Disposes the <see cref="AliveKeeper{T}"/>.
	/// Shuts down all keep alive loops and waits for them to finish.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
