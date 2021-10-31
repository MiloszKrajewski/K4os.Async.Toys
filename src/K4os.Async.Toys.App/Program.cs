using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// ReSharper disable UnusedParameter.Local

namespace K4os.Async.Toys.App
{
	internal static class Program
	{
		public static Task Main(string[] args)
		{
			var loggerFactory = new LoggerFactory();
			loggerFactory.AddProvider(new ColorConsoleProvider());
			var serviceCollection = new ServiceCollection();
			serviceCollection.AddSingleton<ILoggerFactory>(loggerFactory);

			Configure(serviceCollection);
			var serviceProvider = serviceCollection.BuildServiceProvider();
			return Execute(loggerFactory, serviceProvider, args);
		}

		private static void Configure(ServiceCollection serviceCollection) { }

		private static async Task Execute(
			ILoggerFactory loggerFactory, IServiceProvider serviceProvider, string[] args)
		{
			await Task.Yield();

			var log = loggerFactory.CreateLogger("main");

			var keeper = new AliveKeeper<int>(
				x => FakeTouch(log, x),
				x => FakeDelete(log, x),
				x => x.ToString(),
				new AliveKeeperSettings {
					TouchInterval = TimeSpan.FromSeconds(10),
					RetryInterval = TimeSpan.FromSeconds(1),
					TouchBatchSize = 4,
					DeleteBatchSize = 4,
					TouchBatchDelay = TimeSpan.FromSeconds(1),
				},
				loggerFactory.CreateLogger("AliveKeeper"));

			while (true)
			{
				var line = Console.ReadLine();
				if (line is null or "q") break;

				try
				{
					var command = line[0];
					var argument = int.Parse(line[1..]);
					
					switch (command)
					{
						case 's': 
							keeper.Upkeep(argument);
							break;
						case 'd':
							await keeper.Delete(argument);
							break;
					}
				}
				catch (Exception e)
				{
					log.LogError(e, $"Failed to execute: {line}");
				}
			}

			keeper.Dispose();
			
			log.LogInformation("Done");
		}

		private static async Task<int[]> FakeTouch(ILogger log, int[] ids)
		{
			log.LogDebug($"Touch: {string.Join(",", ids)}");
			await Task.Delay(TimeSpan.FromSeconds(0.5));
			return ids;
		}

		private static async Task<int[]> FakeDelete(ILogger log, int[] ids)
		{
			log.LogDebug($"Delete: {string.Join(",", ids)}");
			await Task.Delay(TimeSpan.FromSeconds(0.5));
			return ids;
		}
	}
}
