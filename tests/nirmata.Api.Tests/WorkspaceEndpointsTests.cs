using System.Net;
using System.Net.Http.Json;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.Workspaces;
using Xunit;

namespace nirmata.Api.Tests;

public class WorkspaceEndpointsTests : IClassFixture<nirmataApiFactory>
{
    private readonly HttpClient _client;

    public WorkspaceEndpointsTests(nirmataApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RegisterWorkspace_ReturnsCreatedWorkspace()
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "My Workspace",
            Path = @"C:\Workspaces\my-workspace"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.NotNull(workspace);
        Assert.Equal("My Workspace", workspace!.Name);
        Assert.NotEqual(Guid.Empty, workspace.Id);
        Assert.False(string.IsNullOrWhiteSpace(workspace.Path));
        Assert.False(string.IsNullOrWhiteSpace(workspace.Status));
    }

    [Fact]
    public async Task RegisterWorkspace_LocationHeaderPointsToWorkspace()
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "Location Test",
            Path = @"C:\Workspaces\location-test"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.NotNull(response.Headers.Location);
        Assert.Contains(workspace!.Id.ToString(), response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task RegisterWorkspace_WithRelativePath_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "Relative Path",
            Path = "relative/path"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterWorkspace_WithMissingName_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new { Path = @"C:\Workspaces\no-name" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterWorkspace_WithMissingPath_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/v1/workspaces", new { Name = "No Path" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkspaces_ReturnsOkWithList()
    {
        var response = await _client.GetAsync("/v1/workspaces");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var workspaces = await response.Content.ReadFromJsonAsync<List<WorkspaceSummary>>();
        Assert.NotNull(workspaces);
    }

    [Fact]
    public async Task GetWorkspaces_IncludesRegisteredWorkspace()
    {
        var createResponse = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "List Test Workspace",
            Path = @"C:\Workspaces\list-test"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<WorkspaceSummary>();

        var listResponse = await _client.GetAsync("/v1/workspaces");
        var workspaces = await listResponse.Content.ReadFromJsonAsync<List<WorkspaceSummary>>();

        Assert.Contains(workspaces!, w => w.Id == created!.Id);
    }

    // ── Get by ID ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWorkspaceById_ReturnsWorkspace()
    {
        var createResponse = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "Get By ID",
            Path = @"C:\Workspaces\get-by-id"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<WorkspaceSummary>();

        var response = await _client.GetAsync($"/v1/workspaces/{created!.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var workspace = await response.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.NotNull(workspace);
        Assert.Equal(created.Id, workspace!.Id);
        Assert.Equal(created.Name, workspace.Name);
    }

    [Fact]
    public async Task GetWorkspaceById_UnknownId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/v1/workspaces/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWorkspaceById_InvalidId_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/v1/workspaces/not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateWorkspace_ReturnsUpdatedWorkspace()
    {
        var createResponse = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "Update Test",
            Path = @"C:\Workspaces\update-original"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<WorkspaceSummary>();

        var updateResponse = await _client.PutAsJsonAsync(
            $"/v1/workspaces/{created!.Id}",
            new WorkspaceUpdateRequest { Path = @"C:\Workspaces\update-new" });

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<WorkspaceSummary>();
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated!.Id);
        Assert.Contains("update-new", updated.Path);
    }

    [Fact]
    public async Task UpdateWorkspace_UnknownId_ReturnsNotFound()
    {
        var response = await _client.PutAsJsonAsync(
            $"/v1/workspaces/{Guid.NewGuid()}",
            new WorkspaceUpdateRequest { Path = @"C:\Workspaces\does-not-matter" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateWorkspace_WithRelativePath_ReturnsBadRequest()
    {
        var createResponse = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "Update Relative Path",
            Path = @"C:\Workspaces\update-relative-original"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<WorkspaceSummary>();

        var updateResponse = await _client.PutAsJsonAsync(
            $"/v1/workspaces/{created!.Id}",
            new WorkspaceUpdateRequest { Path = "relative/path" });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteWorkspace_ReturnsNoContent()
    {
        var createResponse = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "Delete Test",
            Path = @"C:\Workspaces\delete-test"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<WorkspaceSummary>();

        var deleteResponse = await _client.DeleteAsync($"/v1/workspaces/{created!.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkspace_DeletedWorkspaceNotFoundOnGet()
    {
        var createResponse = await _client.PostAsJsonAsync("/v1/workspaces", new WorkspaceCreateRequest
        {
            Name = "Delete Then Get",
            Path = @"C:\Workspaces\delete-then-get"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<WorkspaceSummary>();

        await _client.DeleteAsync($"/v1/workspaces/{created!.Id}");

        var getResponse = await _client.GetAsync($"/v1/workspaces/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteWorkspace_UnknownId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/v1/workspaces/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
