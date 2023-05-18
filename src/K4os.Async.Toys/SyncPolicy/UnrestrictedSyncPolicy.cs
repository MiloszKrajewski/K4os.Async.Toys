namespace K4os.Async.Toys.SyncPolicy;

internal class UnrestrictedSyncPolicy: IAliveKeeperSyncPolicy
{
	public static UnrestrictedSyncPolicy Instance { get; } = new();
	
	public Task EnterTouch() => Task.CompletedTask;
	public void LeaveTouch() { }
	public Task EnterDelete() => Task.CompletedTask;
	public void LeaveDelete() { }
	public void Dispose() { }
}