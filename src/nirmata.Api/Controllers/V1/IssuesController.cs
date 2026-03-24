using nirmata.Data.Dto.Models.Spec;
using nirmata.Data.Dto.Requests.Issues;
using nirmata.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace nirmata.Api.Controllers.V1;

[Route("v1/workspaces/{workspaceId:guid}/issues")]
public class IssuesController : nirmataController
{
    private readonly IWorkspaceService _workspaceService;
    private readonly IIssueService _issueService;

    public IssuesController(IWorkspaceService workspaceService, IIssueService issueService)
    {
        _workspaceService = workspaceService;
        _issueService = issueService;
    }

    /// <summary>
    /// Returns all issues from <c>.aos/spec/issues/</c> for the given workspace.
    /// Optional filters: status, severity, taskId, phaseId, milestoneId.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IssueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIssues(
        Guid workspaceId,
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] string? taskId,
        [FromQuery] string? phaseId,
        [FromQuery] string? milestoneId,
        CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var issues = await _issueService.GetAllAsync(root, status, severity, phaseId, taskId, milestoneId, cancellationToken);
        return Ok(issues);
    }

    /// <summary>
    /// Returns a single issue by id.
    /// </summary>
    [HttpGet("{issueId}")]
    [ProducesResponseType(typeof(IssueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetIssue(Guid workspaceId, string issueId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var issue = await _issueService.GetByIdAsync(root, issueId, cancellationToken);
        if (issue is null)
            return NotFoundResult($"Issue '{issueId}' not found in workspace '{workspaceId}'.");

        return Ok(issue);
    }

    /// <summary>
    /// Creates a new issue under <c>.aos/spec/issues/</c> and returns the created record.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(IssueDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateIssue(Guid workspaceId, [FromBody] IssueCreateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationErrorResult();

        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var issue = await _issueService.CreateAsync(root, request, cancellationToken);
        return CreatedResult(nameof(GetIssue), new { workspaceId, issueId = issue.Id }, issue);
    }

    /// <summary>
    /// Replaces the mutable fields of an existing issue.
    /// </summary>
    [HttpPut("{issueId}")]
    [ProducesResponseType(typeof(IssueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateIssue(Guid workspaceId, string issueId, [FromBody] IssueUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationErrorResult();

        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var issue = await _issueService.UpdateAsync(root, issueId, request, cancellationToken);
        if (issue is null)
            return NotFoundResult($"Issue '{issueId}' not found in workspace '{workspaceId}'.");

        return Ok(issue);
    }

    /// <summary>
    /// Deletes an issue from the workspace.
    /// </summary>
    [HttpDelete("{issueId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteIssue(Guid workspaceId, string issueId, CancellationToken cancellationToken)
    {
        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var deleted = await _issueService.DeleteAsync(root, issueId, cancellationToken);
        if (!deleted)
            return NotFoundResult($"Issue '{issueId}' not found in workspace '{workspaceId}'.");

        return NoContentResult();
    }

    /// <summary>
    /// Updates only the status field of an existing issue.
    /// </summary>
    [HttpPatch("{issueId}/status")]
    [ProducesResponseType(typeof(IssueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateIssueStatus(Guid workspaceId, string issueId, [FromBody] IssueStatusUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationErrorResult();

        var root = await _workspaceService.ResolveRootAsync(workspaceId, cancellationToken);
        if (root is null)
            return NotFoundResult($"Workspace '{workspaceId}' not found.");

        var issue = await _issueService.UpdateStatusAsync(root, issueId, request.Status, cancellationToken);
        if (issue is null)
            return NotFoundResult($"Issue '{issueId}' not found in workspace '{workspaceId}'.");

        return Ok(issue);
    }
}
