using Microsoft.Extensions.Hosting;
using Serilog;

namespace Thinktecture.Relay.Connector.Windows;

public class Program
{
	public static async Task<int> Main(string[] args)
	{
		try
		{
			var host = CreateHostBuilder(args).Build();

			await host.RunAsync();
		}
		catch (Exception ex)
		{
			Console.WriteLine("A fatal error cause service crash: {0}", ex);
			Log.Fatal(ex, "A fatal error cause service crash");
			return 1;
		}
		finally
		{
			Log.CloseAndFlush();
		}

		return 0;
	}


	public static IHostBuilder CreateHostBuilder(string[] args)
		=> Host
			.CreateDefaultBuilder(args)
			.UseConsoleLifetime()
			.UseWindowsService()
			.UseSerilog((context, loggerConfiguration) =>
			{
				var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "RelayConnector", "Logs");
				Directory.CreateDirectory(logDir);
				loggerConfiguration
					.MinimumLevel.Debug()
					.MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
					.MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
					// transport-level diagnostics for the hub connection - a stalled or half-dead connection
					// shows up here and nowhere else
					.MinimumLevel.Override("Microsoft.AspNetCore.SignalR.Client", Serilog.Events.LogEventLevel.Information)
					.MinimumLevel.Override("Microsoft.AspNetCore.Http.Connections.Client",
						Serilog.Events.LogEventLevel.Information)
					.WriteTo.File(
						path: Path.Combine(logDir, "connector-.log"),
						rollingInterval: RollingInterval.Day,
						retainedFileCountLimit: 7,
						shared: true)
					.WriteTo.Console();
			})
			.ConfigureServices(Startup.ConfigureServices);
}