using nirmata.Data.Dto.Models.State;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/state")]
public class StateController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IStateService _stateService;

    public StateController(IWorkspaceService workspaceService, IStateService stateService)
    {
        _workspaceService = workspaceService;
        _stateService = stateService;
    }

    /// <summary>
    /// Returns the continuity state from <c>.aos/state/state.json</c> for the given workspace.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ContinuityStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetState(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var state = await _stateService.GetStateAsync(root, cancellationToken);
        if (state is null)
            return NotFoundResult($"State file not found for workspace '{workspaceId}'.");

        return Ok(state);
    }

    /// <summary>
    /// Returns the pause/resume handoff snapshot from <c>.aos/state/handoff.json</c>.
    /// Returns 404 when no handoff snapshot exists (workspace is not paused).
    /// </summary>
    [HttpGet("handoff")]
    [ProducesResponseType(typeof(HandoffSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHandoff(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var handoff = await _stateService.GetHandoffAsync(root, cancellationToken);
        if (handoff is null)
            return NotFoundResult($"No handoff snapshot found for workspace '{workspaceId}'.");

        return Ok(handoff);
    }

    /// <summary>
    /// Returns the most recent events from <c>.aos/state/events.ndjson</c>.
    /// Use the optional <paramref name="limit"/> query parameter to control how many events are returned (default 50).
    /// </summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<StateEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEvents(
        Guid workspaceId,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var events = await _stateService.GetEventsAsync(root, limit, cancellationToken);
        return Ok(events);
    }
}
