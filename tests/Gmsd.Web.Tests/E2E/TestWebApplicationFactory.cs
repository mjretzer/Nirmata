using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// Factory for creating a test web application for E2E tests.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the default configuration and add test-specific configuration
            // Keep existing services but override configuration as needed for tests
        });
    }
}
