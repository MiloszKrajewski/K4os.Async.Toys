//using System.Collections.Concurrent;
//using System.Diagnostics.CodeAnalysis;
//using System.Threading.Channels;
//using K4os.Async.Toys;
//using Microsoft.Extensions.Logging;
//
//namespace Playground;
//
//public class PriorityAliveKeeper<T>: IAliveKeeper<T> where T: notnull
//{
//	private readonly Func<T[], Task<T[]>> _touchAction;
//	private readonly Func<T[], Task<T[]>>? _deleteAction;
//
//	// ReSharper disable once StaticMemberInGenericType
//	private static readonly UnboundedChannelOptions QueueOptions = new() {
//		SingleReader = true,
//		SingleWriter = false,
//	};
//
//	private ConcurrentDictionary<T, Meta> _meta = new();
//	private Channel<Item> _toBeDeleted = Channel.CreateUnbounded<Item>(QueueOptions);
//	private Channel<Item> _toBeTouched = Channel.CreateUnbounded<Item>(QueueOptions);
//	
//	private readonly Agent<Item> _deleteAgent;
//	private readonly Agent<Item> _touchAgent;
//	private readonly Func<T, string> _keyToString;
//
//	public PriorityAliveKeeper(
//		Func<T[], Task<T[]>> touchAction,
//		Func<T[], Task<T[]>>? deleteAction,
//		Func<T, string>? keyToString,
//		IAliveKeeperSettings? settings = null,
//		ILogger? log = null,
//		ITimeSource? time = null)
//	{
//		settings ??= new AliveKeeperSettings();
//		var concurrency = settings.Concurrency;
//
//		var touchGate = new SemaphoreSlim(concurrency);
//		var deleteGate = new SemaphoreSlim(concurrency);
//
//		_touchAction = touchAction ?? throw new ArgumentNullException(nameof(touchAction));
//		_deleteAction = deleteAction;
//		_keyToString = keyToString ?? (k => k.ToString() ?? "<null>");
//
//		_touchAgent = Agent.Create<Item>(c => TouchManyLoop(c, touchGate));
//		_deleteAgent = Agent.Create<Item>(c => DeleteLoop(c, deleteGate));
//	}
//
//	private string KeyToString(T key) => _keyToString(key);
//
//	private async Task TouchOneLoop(Meta meta, CancellationToken token)
//	{
//		token = CancellationTokenSource.CreateLinkedTokenSource(token, meta.Token).Token;
//
//		while (!token.IsCancellationRequested)
//		{
//			await Task.Delay(1000, token);
//			var touched = meta.Touched;
//			_touchAgent.Enqueue(new Item(meta));
//			await touched;
//		}
//	}
//
//	private async Task TouchManyLoop(IAgentContext<Item> context, SemaphoreSlim gate)
//	{
//		var token = context.Token;
//		var reader = context.Queue.Reader;
//		var batchSize = 10;
//
//		while (!token.IsCancellationRequested)
//		{
//			var available = await reader.WaitToReadAsync(token);
//			if (!available) continue;
//
//			await gate.WaitAsync(token);
//			var items = DrainTouchQueue(reader, batchSize);
//			Task.Run(() => TouchMany(items, gate, token), token).Forget();
//		}
//	}
//
//	private List<Meta>? DrainTouchQueue(ChannelReader<Item> reader, int batchSize)
//	{
//		var result = default(List<Meta>?);
//		var count = batchSize;
//
//		while (count > 0)
//		{
//			if (!reader.TryRead(out var item)) break;
//
//			if (!TryMarkItemForTouch(item, out var meta)) continue;
//
//			(result ??= new List<Meta>(batchSize)).Add(meta);
//			count--;
//		}
//
//		return result;
//	}
//
//	private bool TryMarkItemForTouch(Item item, [NotNullWhen(true)] out Meta? meta)
//	{
//		// deleted
//		if (!_meta.TryGetValue(item.Key, out meta))
//			return false;
//
//		lock (meta)
//		{
//			// canceled
//			if (meta.Token.IsCancellationRequested)
//				return false;
//
//			return true;
//		}
//	}
//
//	private async Task TouchMany(
//		ICollection<Meta>? items,
//		SemaphoreSlim gate,
//		CancellationToken token)
//	{
//		try
//		{
//			if (items is null) return;
//			//...
//		}
//		finally
//		{
//			gate.Release();
//		}
//	}
//
//	private Task DeleteLoop(IAgentContext<Item> context, SemaphoreSlim gate)
//	{
//		throw new NotImplementedException();
//	}
//
//	public class Item
//	{
//		public T Key { get; }
//
//		public Item(T key) => Key = key;
//		public Item(Meta meta): this(meta.Key) { }
//	}
//
//	public class Meta
//	{
//		public T Key { get; }
//
//		private readonly CancellationTokenSource _cancel = new();
//		private TaskCompletionSource _touched = new();
//		private readonly TaskCompletionSource _deleted = new();
//
//		public CancellationToken Token => _cancel.Token;
//		public void Cancel() => _cancel.Cancel();
//		public Task Deleted => _deleted.Task;
//		public Task Touched => _touched.Task;
//
//		public Meta(T key) => Key = key;
//	}
//
//	public void Register(T item, CancellationToken token = default)
//	{
//		var meta = _meta.AddOrUpdate(
//			item,
//			k => new Meta(k),
//			(k, _) => throw new InvalidOperationException($"Item {KeyToString(k)} already exists"));
//		
//		Task.Run(() => TouchOneLoop(meta, token), token).Forget();
//	}
//
//	public async Task Delete(T item, CancellationToken token = default)
//	{
//		throw new NotImplementedException();
//	}
//
//	public void Forget(T item) { throw new NotImplementedException(); }
//
//	public async Task Shutdown(CancellationToken token = default)
//	{
//		throw new NotImplementedException();
//	}
//
//	protected virtual void Dispose(bool disposing)
//	{
//		if (disposing)
//		{
//			// TODO release managed resources here
//		}
//	}
//
//	public void Dispose()
//	{
//		Dispose(true);
//		GC.SuppressFinalize(this);
//	}
//}
