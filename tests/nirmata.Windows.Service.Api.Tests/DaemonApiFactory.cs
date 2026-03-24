using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using nirmata.Windows.Service.Api;

namespace nirmata.Windows.Service.Api.Tests;

/// <summary>
/// WebApplicationFactory for the daemon API. Each test class that uses
/// IClassFixture&lt;DaemonApiFactory&gt; gets its own factory instance, so
/// DaemonRuntimeState is isolated between test classes.
/// </summary>
public sealed class DaemonApiFactory : WebApplicationFactory<Program>
{
    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    /// <summary>
    /// Returns the shared DaemonRuntimeState so tests can pre-seed data
    /// or inspect mutations after controller actions.
    /// </summary>
    public DaemonRuntimeState GetRuntimeState() =>
        Services.GetRequiredService<DaemonRuntimeState>();
}
