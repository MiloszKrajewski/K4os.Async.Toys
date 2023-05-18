namespace K4os.Async.Toys.SyncPolicy;

internal interface IAliveKeeperSyncPolicy: IDisposable
{
	Task EnterTouch();
	void LeaveTouch();

	Task EnterDelete();
	void LeaveDelete();
}
