namespace K4os.Async.Toys.SyncPolicy;

internal class SafeSyncPolicy: IAliveKeeperSyncPolicy
{
	private readonly SemaphoreSlim _semaphore = new(0, 1);
	public Task EnterTouch() => _semaphore.WaitAsync();
	public void LeaveTouch() => _semaphore.Release();
	public Task EnterDelete() => _semaphore.WaitAsync();
	public void LeaveDelete() => _semaphore.Release();
	public void Dispose() => _semaphore.Dispose();
}
