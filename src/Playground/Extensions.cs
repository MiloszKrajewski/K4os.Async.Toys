namespace Playground;

public static class Extensions
{
	public static void Forget(this Task task)
	{
		task.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.NotOnRanToCompletion);
	}
}
