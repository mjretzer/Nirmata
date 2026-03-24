using nirmata.Data.Dto.Models.Spec;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/uat")]
public class UatController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IUatService _uatService;

    public UatController(IWorkspaceService workspaceService, IUatService uatService)
    {
        _workspaceService = workspaceService;
        _uatService = uatService;
    }

    /// <summary>
    /// Returns all UAT records from <c>.aos/spec/uat/</c> and <c>.aos/spec/tasks/TSK-*/uat.json</c>
    /// for the given workspace, together with derived pass/fail summaries per task and phase.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(UatSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUatSummary(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var summary = await _uatService.GetSummaryAsync(root, cancellationToken);
        return Ok(summary);
    }
}
