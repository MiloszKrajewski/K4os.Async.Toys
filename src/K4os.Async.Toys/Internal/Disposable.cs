namespace K4os.Async.Toys.Internal;

/// <summary>
/// Disposable that executes given action when disposed.
/// </summary>
public class Disposable: IDisposable
{
	/// <summary>Creates disposable that executes given action when disposed.</summary>
	/// <param name="action">Action to be executed. Note, <c>null</c> is a valid value for no-action.</param>
	/// <returns>Disposable.</returns>
	public static IDisposable Create(Action? action = null) => new Disposable(action);

	private readonly Action? _action;

	/// <summary>Creates disposable that executes given action when disposed.</summary>
	/// <param name="action">Action to be executed. Note, <c>null</c> is a valid value for no-action.</param>
	public Disposable(Action? action = null) => _action = action;

	/// <summary>Executes wrapped action.</summary>
	public void Dispose() => _action?.Invoke();
}
