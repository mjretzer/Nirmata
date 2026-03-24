using nirmata.Data.Dto.Models.State;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/checkpoints")]
public class CheckpointsController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IStateService _stateService;

    public CheckpointsController(IWorkspaceService workspaceService, IStateService stateService)
    {
        _workspaceService = workspaceService;
        _stateService = stateService;
    }

    /// <summary>
    /// Returns checkpoint summaries from <c>.aos/state/checkpoints/**</c> for the given workspace, newest first.
    /// Returns an empty array when no checkpoints exist.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CheckpointSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCheckpoints(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var checkpoints = await _stateService.GetCheckpointsAsync(root, cancellationToken);
        return Ok(checkpoints);
    }
}
