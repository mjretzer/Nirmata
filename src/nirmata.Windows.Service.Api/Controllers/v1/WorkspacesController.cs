using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class WorkspaceSummary
{
    public string Id { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime LastModified { get; init; }
}

[ApiController]
[Route("api/v1/workspaces")]
public class WorkspacesController : ControllerBase
{
    private static readonly List<WorkspaceSummary> _workspaces = [];

    /// <summary>
    /// Returns all registered workspaces.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<WorkspaceSummary>), StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(_workspaces);

    /// <summary>
    /// Returns a single workspace by id.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(WorkspaceSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(string id)
    {
        var workspace = _workspaces.FirstOrDefault(w => w.Id == id);
        return workspace is null ? NotFound() : Ok(workspace);
    }
}
