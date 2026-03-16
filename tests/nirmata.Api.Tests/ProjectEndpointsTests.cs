using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Models.Projects;
using nirmata.Data.Dto.Requests.Projects;
using Xunit;

namespace nirmata.Api.Tests;

public class ProjectEndpointsTests : IClassFixture<nirmataApiFactory>
{
    private readonly HttpClient _client;

    public ProjectEndpointsTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateProject_ReturnsCreatedProject()
    {
        var request = new ProjectCreateRequestDto
        {
            Name = "API Project"
        };

        var response = await _client.PostAsJsonAsync("/v1/projects", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProjectResponseDto>();
        Assert.NotNull(payload);
        Assert.Equal("API Project", payload!.Name);
        Assert.False(string.IsNullOrWhiteSpace(payload.ProjectId));
    }

    [Fact]
    public async Task GetProjectById_ReturnsProject()
    {
        var createResponse = await _client.PostAsJsonAsync("/v1/projects", new ProjectCreateRequestDto
        {
            Name = "Readback Project"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectResponseDto>();

        var response = await _client.GetAsync($"/v1/projects/{created!.ProjectId}");
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ProjectResponseDto>();
        Assert.Equal(created.ProjectId, payload!.ProjectId);
    }

    [Fact]
    public async Task GetProjectById_ReturnsNotFoundWhenMissing()
    {
        var response = await _client.GetAsync("/v1/projects/missing-project");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_ReturnsValidationErrors()
    {
        var response = await _client.PostAsJsonAsync("/v1/projects", new { });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
