using nirmata.Data.Dto.Models.Codebase;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/codebase")]
public class CodebaseController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly ICodebaseService _codebaseService;

    public CodebaseController(IWorkspaceService workspaceService, ICodebaseService codebaseService)
    {
        _workspaceService = workspaceService;
        _codebaseService = codebaseService;
    }

    /// <summary>
    /// Returns the codebase artifact inventory, language breakdown, and stack metadata
    /// derived from the workspace's <c>.aos/codebase/</c> directory.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(CodebaseInventoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInventory(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var inventory = await _codebaseService.GetInventoryAsync(root, cancellationToken);
        return Ok(inventory);
    }

    /// <summary>
    /// Returns the parsed payload for a single codebase artifact by id
    /// (e.g. "map", "stack", "architecture").
    /// Returns 404 for unknown artifact ids.
    /// </summary>
    [HttpGet("{artifactId}")]
    [ProducesResponseType(typeof(CodebaseArtifactDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetArtifact(
        Guid workspaceId,
        string artifactId,
        CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var artifact = await _codebaseService.GetArtifactAsync(root, artifactId, cancellationToken);
        if (artifact is null)
            return NotFoundResult($"Artifact '{artifactId}' not found.");

        return Ok(artifact);
    }
}
