using Xunit;

namespace K4os.Async.Toys.Test;

public class AliveKeeperTests
{
	[Fact]
	public void DoubleDisposeShouldNotFail()
	{
		var keeper = new AliveKeeper<int>(Task.FromResult, Task.FromResult);
		keeper.Dispose();
		keeper.Dispose();
		keeper.Dispose();
	}
}
