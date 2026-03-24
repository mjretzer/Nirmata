using nirmata.Data.Dto.Models.Evidence;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/runs")]
public class RunsController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IEvidenceService _evidenceService;

    public RunsController(IWorkspaceService workspaceService, IEvidenceService evidenceService)
    {
        _workspaceService = workspaceService;
        _evidenceService = evidenceService;
    }

    /// <summary>
    /// Returns all run summaries from <c>.aos/evidence/runs/</c> for the given workspace, newest first.
    /// Returns an empty array when no runs exist.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RunSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRuns(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var runs = await _evidenceService.GetRunsAsync(root, cancellationToken);
        return Ok(runs);
    }

    /// <summary>
    /// Returns the detail for a single run by id, including commands, log files, and artifacts.
    /// Returns 404 when the run does not exist.
    /// </summary>
    [HttpGet("{runId}")]
    [ProducesResponseType(typeof(RunDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRun(Guid workspaceId, string runId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var run = await _evidenceService.GetRunAsync(root, runId, cancellationToken);
        if (run is null)
            return NotFoundResult($"Run '{runId}' not found in workspace '{workspaceId}'.");

        return Ok(run);
    }
}
