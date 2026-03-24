using nirmata.Data.Dto.Models.State;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/state/packs")]
public class ContextPacksController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IStateService _stateService;

    public ContextPacksController(IWorkspaceService workspaceService, IStateService stateService)
    {
        _workspaceService = workspaceService;
        _stateService = stateService;
    }

    /// <summary>
    /// Returns context-pack summaries from <c>.aos/context/packs/**</c> for the given workspace.
    /// Returns an empty array when no packs exist.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ContextPackSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContextPacks(Guid workspaceId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var packs = await _stateService.GetContextPacksAsync(root, cancellationToken);
        return Ok(packs);
    }
}
