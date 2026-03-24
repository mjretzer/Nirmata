using nirmata.Data.Dto.Models.OrchestratorGate;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/orchestrator")]
public class OrchestratorController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IOrchestratorGateService _orchestratorGateService;

    public OrchestratorController(
        IWorkspaceService workspaceService,
        IOrchestratorGateService orchestratorGateService)
    {
        _workspaceService = workspaceService;
        _orchestratorGateService = orchestratorGateService;
    }

    /// <summary>
    /// Returns the current orchestrator gate for the workspace — identifying the next task,
    /// evaluating dependency, evidence, and UAT checks, and indicating whether the workspace
    /// is ready to advance.
    /// </summary>
    [HttpGet("gate")]
    [ProducesResponseType(typeof(OrchestratorGateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGate(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var gate = await _orchestratorGateService.GetGateAsync(root, cancellationToken);
        return Ok(gate);
    }

    /// <summary>
    /// Returns the ordered orchestrator timeline for the workspace.
    /// Each step contains a stable <c>id</c>, <c>label</c>, and <c>status</c>.
    /// </summary>
    [HttpGet("timeline")]
    [ProducesResponseType(typeof(OrchestratorTimelineDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var timeline = await _orchestratorGateService.GetTimelineAsync(root, cancellationToken);
        return Ok(timeline);
    }
}
