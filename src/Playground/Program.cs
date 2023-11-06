using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Playground;

var host = Host
	.CreateDefaultBuilder(args)
	.BootstrapSerilog()
	.ConfigureServices((ctx, svc) => { })
	.Build();

await Main();
return;

async Task Main()
{
	await Task.CompletedTask;
	var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
	
	var sut = new SubscriberStressTest(loggerFactory);
	await sut.ConstantStreamOfMessages(TimeSpan.FromSeconds(10));
}
