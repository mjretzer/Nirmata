using System.IO;
using DotNetEnv;
using nirmata.Agents.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace nirmata.Agents;

/// <summary>
/// Host entrypoint for the nirmata Agent Plane.
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
                LoadDotEnvIfPresent();
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables(prefix: "nirmata_");
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                services.AddnirmataAgents(context.Configuration);
            })
            .ConfigureLogging((context, logging) =>
            {
                logging.AddConsole();
                logging.AddDebug();
                logging.SetMinimumLevel(context.HostingEnvironment.IsDevelopment() 
                    ? LogLevel.Debug 
                    : LogLevel.Information);
            });

    private static void LoadDotEnvIfPresent()
    {
        var currentDirectory = Directory.GetCurrentDirectory();

        while (!string.IsNullOrEmpty(currentDirectory))
        {
            var envPath = Path.Combine(currentDirectory, ".env");
            if (File.Exists(envPath))
            {
                Env.Load(envPath);
                break;
            }

            var parentDirectory = Directory.GetParent(currentDirectory);
            if (parentDirectory is null || parentDirectory.FullName == currentDirectory)
            {
                break;
            }

            currentDirectory = parentDirectory.FullName;
        }
    }
}
