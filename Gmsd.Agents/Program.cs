using Gmsd.Agents.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gmsd.Agents;

/// <summary>
/// Host entrypoint for the GMSD Agent Plane.
/// Boots configuration, DI, and starts the agent runtime.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables(prefix: "GMSD_");
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddGmsdAgents(context.Configuration);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.AddConsole();
                logging.AddDebug();
                logging.SetMinimumLevel(context.HostingEnvironment.IsDevelopment() 
                    ? LogLevel.Debug 
                    : LogLevel.Information);
            });
}
