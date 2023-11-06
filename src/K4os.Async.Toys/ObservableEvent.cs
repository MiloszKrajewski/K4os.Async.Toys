using K4os.Async.Toys.Internal;

namespace K4os.Async.Toys;

/// <summary>
/// Simple implementation of <see cref="IObservable{T}"/> and <see cref="IObserver{T}"/>.
/// This definitely does not replace Reactive Extension, but allow to use <see cref="IObservable{T}"/>
/// in this library without adding dependency on Rx.
/// </summary>
/// <typeparam name="T">Type of event.</typeparam>
public class ObservableEvent<T>: IObserver<T>, IObservable<T>
{
	private long _version;
	private readonly Dictionary<long, IObserver<T>> _observers = new();
	
	/// <summary>
	/// Subscribes observer to this observable.
	/// </summary>
	/// <param name="observer">Observer.</param>
	/// <returns>Subscription.</returns>
	public IDisposable Subscribe(IObserver<T> observer)
	{
		observer.Required();
		
		lock (_observers)
		{
			var version = _version++;
			_observers.Add(version, observer);
			return Disposable.Create(() => Unsubscribe(version));
		}
	}

	private void Unsubscribe(long version)
	{
		lock (_observers)
		{
			_observers.Remove(version);
		}
	}
	
	private IObserver<T>[] Observers()
	{
		lock (_observers)
		{
			return _observers.Values.ToArray();
		}
	}

	/// <inheritdoc />
	public void OnNext(T value)
	{
		var observers = Observers();
		observers.ForEach(o => o.OnNext(value));
	}

	/// <inheritdoc />
	public void OnCompleted()
	{
		var observers = Observers();
		observers.ForEach(o => o.OnCompleted());
	}

	/// <inheritdoc />
	public void OnError(Exception error)
	{
		var observers = Observers();
		observers.ForEach(o => o.OnError(error));
	}
}
