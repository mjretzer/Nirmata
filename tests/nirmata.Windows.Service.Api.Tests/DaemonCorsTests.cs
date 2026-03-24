using System.Net;
using Xunit;

namespace nirmata.Windows.Service.Api.Tests;

public class DaemonCorsTests : IClassFixture<DaemonApiFactory>
{
    private readonly HttpClient _client;

    public DaemonCorsTests(DaemonApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/api/v1/health", "GET")]
    [InlineData("/api/v1/commands", "POST")]
    public async Task OptionsPreflight_FromLocalFrontendOrigin_Returns204AndAllowsOrigin(string path, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", "http://localhost:5173");
        request.Headers.Add("Access-Control-Request-Method", method);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowOrigins));
        Assert.Contains("http://localhost:5173", allowOrigins);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Methods", out var allowMethods));
        Assert.Contains(method, allowMethods.First());
    }
}
