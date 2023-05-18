namespace K4os.Async.Toys.SyncPolicy;

internal class AlternatingSyncPolicy: IAliveKeeperSyncPolicy
{
	private class ActionState
	{
		public int Waiting;
		public int Active;
		public int Granted;
	}

	// this is more for readability and debugging than anything else
	private class DeleteActionState: ActionState { }
	private class TouchActionState: ActionState { }

	private readonly object _lock = new();
	private readonly ManualResetSignal _pulse = new();

	private ActionState? _currentState;
	private readonly ActionState _touch;
	private readonly ActionState _delete;

	public AlternatingSyncPolicy()
	{
		_pulse.Set();
		_currentState = null;
		_touch = new TouchActionState();
		_delete = new DeleteActionState();
	}
	
	private async Task EnterAction(ActionState thisState, ActionState otherState)
	{
		var attempt = 0;
		while (true)
		{
			var success = TryEnterAction(attempt, thisState, otherState);
			if (success) return;

			await _pulse.WaitAsync();
			attempt++;
		}
	}

	private bool TryEnterAction(
		int attempt,
		ActionState thisState,
		ActionState otherState)
	{
		lock (_lock)
		{
			var freeEntry =
				_currentState is null ||
				(_currentState == thisState && otherState.Waiting == 0);

			var grantedEntry =
				!freeEntry && _currentState == thisState && thisState.Granted > 0;
			
			if (!freeEntry && !grantedEntry)
			{
				if (attempt == 0) thisState.Waiting++;
				return false;
			}
			
			if (grantedEntry) thisState.Granted--;
			_currentState = thisState;
			thisState.Active++;
			_pulse.Reset();
			return true;
		}
	}

	private void LeaveAction(ActionState thisState, ActionState otherState)
	{
		lock (_lock)
		{
			var stillActive = --thisState.Active > 0;
			
			if (stillActive) return;

			if (otherState.Waiting <= 0)
			{
				_currentState = null;
			}
			else
			{
				// switch to other state and allow all waiting entries, but not more
				_currentState = otherState;
				if (thisState.Waiting > 0) 
					otherState.Granted = otherState.Waiting;
			}

			_pulse.Set();
		}
	}

	public Task EnterTouch() => EnterAction(_touch, _delete);
	public void LeaveTouch() => LeaveAction(_touch, _delete);

	public Task EnterDelete() => EnterAction(_delete, _touch);
	public void LeaveDelete() => LeaveAction(_delete, _touch);

	public void Dispose() { }
}
