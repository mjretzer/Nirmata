using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Requests.Issues;
using Xunit;

namespace nirmata.Api.Tests;

/// <summary>
/// Verifies that all <c>/v1/workspaces/{workspaceId}/issues</c> endpoints return
/// 404 Not Found when the workspace ID is unknown.
/// </summary>
public class IssueEndpointsTests : IClassFixture<nirmataApiFactory>
{
    private readonly HttpClient _client;

    public IssueEndpointsTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET /issues ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetIssues_UnknownWorkspaceId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/issues");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /issues/{issueId} ──────────────────────────────────────────────────

    [Fact]
    public async Task GetIssue_UnknownWorkspaceId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}/issues/ISS-0001");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── POST /issues ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateIssue_UnknownWorkspaceId_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync(
            $"/v1/workspaces/{Guid.NewGuid()}/issues",
            new IssueCreateRequest { Title = "Test issue" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PUT /issues/{issueId} ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdateIssue_UnknownWorkspaceId_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync(
            $"/v1/workspaces/{Guid.NewGuid()}/issues/ISS-0001",
            new IssueUpdateRequest { Title = "Updated title" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE /issues/{issueId} ───────────────────────────────────────────────

    [Fact]
    public async Task DeleteIssue_UnknownWorkspaceId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/v1/workspaces/{Guid.NewGuid()}/issues/ISS-0001");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PATCH /issues/{issueId}/status ────────────────────────────────────────

    [Fact]
    public async Task UpdateIssueStatus_UnknownWorkspaceId_ReturnsNotFound()
    {
        var response = await _client.PatchAsJsonAsync(
            $"/v1/workspaces/{Guid.NewGuid()}/issues/ISS-0001/status",
            new IssueStatusUpdateRequest { Status = "open" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
