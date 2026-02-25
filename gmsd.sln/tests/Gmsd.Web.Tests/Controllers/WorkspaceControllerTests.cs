using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Gmsd.Aos.Public;
using Gmsd.Data.Repositories;
using Gmsd.Web.Models;
using Gmsd.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Gmsd.Web.Tests.Controllers;

public class WorkspaceControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IWorkspaceService> _workspaceServiceMock;

    public WorkspaceControllerTests(WebApplicationFactory<Program> factory)
    {
        _workspaceServiceMock = new Mock<IWorkspaceService>();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production"); // Disable DI validation
            builder.ConfigureTestServices(services =>
            {
                services.AddScoped(_ => _workspaceServiceMock.Object);
            });
        });
    }

    [Fact]
    public async Task List_ReturnsOkWithWorkspaces()
    {
        // Arrange
        var workspaces = new List<WorkspaceDto>
        {
            new(Guid.NewGuid(), "path1", "name1", DateTimeOffset.UtcNow, "Healthy")
        };
        _workspaceServiceMock.Setup(s => s.ListWorkspacesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaces);
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/workspaces");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<List<WorkspaceDto>>();
        result.Should().HaveCount(1);
        result![0].Name.Should().Be("name1");
    }

    [Fact]
    public async Task Open_ReturnsOkWithWorkspace()
    {
        // Arrange
        var request = new OpenWorkspaceRequest("test-path");
        var workspace = new WorkspaceDto(Guid.NewGuid(), "test-path", "test", DateTimeOffset.UtcNow, "Healthy");
        _workspaceServiceMock.Setup(s => s.OpenWorkspaceAsync("test-path", It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        var client = _factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/workspaces/open", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<WorkspaceDto>();
        result!.Path.Should().Be("test-path");
    }

    [Fact]
    public async Task GetActive_ReturnsActivePath()
    {
        // Arrange
        _workspaceServiceMock.Setup(s => s.GetActiveWorkspacePathAsync())
            .ReturnsAsync("active-path");
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/workspaces/active");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        doc.RootElement.GetProperty("path").GetString().Should().Be("active-path");
    }
}
