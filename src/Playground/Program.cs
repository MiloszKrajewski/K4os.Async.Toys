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

	var sut = new AliveKeeperStress(host.Services.GetRequiredService<ILoggerFactory>());
	await sut.LotsOfUpkeepLoops();
}
