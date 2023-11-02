using Xunit;

namespace K4os.Async.Toys.Test;

public class TokenTests
{
	[Fact]
	public void LinkedTokenCancellationCascade()
	{
		var a = new CancellationTokenSource();
		var b = new CancellationTokenSource();
		var c = new CancellationTokenSource();
		var cts = CancellationTokenSource.CreateLinkedTokenSource(a.Token, b.Token, c.Token).Token;
		
		Assert.False(cts.IsCancellationRequested);
		a.Cancel();
		Assert.True(cts.IsCancellationRequested);
	}
}
