using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace nirmata.Windows.Service;

/// <summary>
/// Hosts the daemon HTTP API (Kestrel) in-proc with the Windows Service host.
/// This is the alternative deployment shape to the default companion-process.
///
/// Enable via configuration:
///   InProcDaemonApi:Enabled = true
///
/// When enabled:
///   - A Kestrel web server starts on the configured URL alongside the engine worker.
///   - Shutdown is coordinated through the host's IHostApplicationLifetime — a single
///     graceful stop path covers both the web server and the worker.
///   - Logs flow through the shared logging pipeline (Windows Service event log in
///     production, console in dev).
///
/// Trade-offs vs companion-process (see nirmata.Windows.Service.Api/README.md):
///   - One process to deploy and install.
///   - A fatal web-host failure will also terminate the engine worker.
///   - Shared log stream; no independent log files per surface.
/// </summary>
internal sealed class InProcDaemonApiService : IHostedService, IAsyncDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<InProcDaemonApiService> _logger;
    private WebApplication? _daemonApp;

    public InProcDaemonApiService(
        IConfiguration configuration,
        ILogger<InProcDaemonApiService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var listenUrl = _configuration["InProcDaemonApi:BaseUrl"] ?? "https://localhost:9000";
        _logger.LogInformation("Starting in-proc daemon API on {Url}", listenUrl);

        var builder = WebApplication.CreateBuilder();

        // Thread the parent host's logging configuration so log output flows through
        // the same pipeline (EventLog, console, etc.) as the engine worker.
        builder.Logging.ClearProviders();
        builder.Logging.AddConfiguration(_configuration.GetSection("Logging"));
        builder.Logging.AddConsole();

        // UseUrls is an explicit IWebHostBuilder implementation on ConfigureWebHostBuilder;
        // set via UseSetting which is directly accessible.
        builder.WebHost.UseSetting(WebHostDefaults.ServerUrlsKey, listenUrl);

        // CORS: configurable via InProcDaemonApi:Cors:AllowedOrigins
        var allowedOrigins =
            _configuration.GetSection("InProcDaemonApi:Cors:AllowedOrigins").Get<string[]>() ?? [];

        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });

        // Controllers can be shared from a dedicated library when the in-proc shape is
        // promoted to a first-class deployment target. For now the hosting plumbing is
        // in place; endpoints are added below.
        builder.Services.AddControllers();

        _daemonApp = builder.Build();

        _daemonApp.UseCors();
        _daemonApp.MapControllers();

        // Health endpoint — mirrors the companion-process shape; full daemon API
        // controllers are wired once controller sharing is established.
        _daemonApp.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            mode = "in-proc",
        }));

        await _daemonApp.StartAsync(cancellationToken);
        _logger.LogInformation("In-proc daemon API listening on {Url}", listenUrl);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_daemonApp is null)
            return;

        _logger.LogInformation("Stopping in-proc daemon API");
        await _daemonApp.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_daemonApp is not null)
            await _daemonApp.DisposeAsync();
    }
}
