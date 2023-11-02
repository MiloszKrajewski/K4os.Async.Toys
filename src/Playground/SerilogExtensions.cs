﻿using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;

namespace Playground;

public static class SerilogExtensions
{
	public static IHostBuilder BootstrapSerilog(this IHostBuilder builder) => 
		builder.UseSerilog(Configure);

	private static void Configure(
		HostBuilderContext context,
		IServiceProvider provider,
		LoggerConfiguration logging)
	{
		const string outputTemplate =
			"[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";
		var targetFile = Path.Combine(
			AppContext.BaseDirectory, "Logs", context.HostingEnvironment.ApplicationName + "-.json");
		logging
			.ReadFrom.Configuration(context.Configuration)
			.WriteTo.Console(outputTemplate: outputTemplate)
			.WriteTo.File(
				new RenderedCompactJsonFormatter(), targetFile,
				rollingInterval: RollingInterval.Day,
				fileSizeLimitBytes: null);
	}
}
