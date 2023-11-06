using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace K4os.Async.Toys.Internal;

/// <summary>A disposable collection of disposables.</summary>
/// <seealso cref="IDisposable" />
internal class DisposableBag: IDisposable
{
	private readonly object _mutex = new();
	private readonly List<IDisposable> _bag = new();
	private bool _disposed;

	/// <summary>Creates disposable collection of disposables.</summary>
	/// <returns>New disposable collection of disposables</returns>
	public static DisposableBag Create() => new();

	/// <summary>Creates disposable collection of disposables starting with provided ones.</summary>
	/// <param name="disposables">The disposables.</param>
	/// <returns>New disposable collection of disposables.</returns>
	public static DisposableBag Create(IEnumerable<IDisposable> disposables) =>
		new(disposables);

	/// <summary>Creates disposable collection of disposables.</summary>
	/// <param name="disposables">The disposables.</param>
	/// <returns>New disposable collection of disposables.</returns>
	public static DisposableBag Create(params IDisposable[] disposables) =>
		new(disposables);

	/// <summary>Initializes a new instance of the <see cref="DisposableBag"/> class.</summary>
	public DisposableBag() { }

	/// <summary>Initializes a new instance of the <see cref="DisposableBag"/> class.</summary>
	/// <param name="disposables">The disposables.</param>
	public DisposableBag(IEnumerable<IDisposable> disposables) => 
		_bag.AddRange(disposables); // safe in constructor

	/// <summary>Adds many disposables.</summary>
	/// <param name="disposables">The disposables.</param>
	public void AddMany(IEnumerable<IDisposable> disposables)
	{
		lock (_mutex)
		{
			if (_disposed)
			{
				DisposeMany(disposables.Reverse());
			}
			else
			{
				_bag.AddRange(disposables);
			}
		}
	}

	/// <summary>Disposes items.</summary>
	/// <param name="disposables">The disposables.</param>
	/// <exception cref="AggregateException">Combines exceptions thrown when disposing items.</exception>
	public static void DisposeMany(IEnumerable<IDisposable> disposables)
	{
		var exceptions = default(List<Exception>);
		foreach (var disposable in disposables)
		{
			try
			{
				disposable.Dispose();
			}
			catch (Exception e)
			{
				(exceptions ??= new List<Exception>()).Add(e);
			}
		}

		if (exceptions != null)
			throw new AggregateException(exceptions);
	}

	/// <summary>Disposes items.</summary>
	/// <param name="disposables">The disposables.</param>
	/// <exception cref="AggregateException">Combines exceptions thrown when disposing items.</exception>
	public static void DisposeMany(params IDisposable[] disposables) => 
		DisposeMany((IEnumerable<IDisposable>)disposables);

	/// <summary>Adds disposable.</summary>
	/// <typeparam name="T">Type of object.</typeparam>
	/// <param name="item">The disposable item.</param>
	/// <returns>Same item for further processing.</returns>
	[return: NotNullIfNotNull("item")]
	public T? Add<T>(T? item) where T: IDisposable
	{
		if (item is null) return item;

		lock (_mutex)
		{
			if (_disposed)
			{
				item.Dispose();
			}
			else
			{
				_bag.Add(item);
			}

			return item;
		}
	}

	/// <summary>Performs application-defined tasks associated with 
	/// freeing, releasing, or resetting unmanaged resources.</summary>
	public void Dispose()
	{
		IDisposable[] disposables;
		lock (_mutex)
		{
			_disposed = true;
			disposables = _bag.ToArray();
			_bag.Clear();
		}

		Array.Reverse(disposables); // dispose in reverse order
		DisposeMany(disposables);
	}
}