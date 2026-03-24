using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class IssueSummary
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? TaskId { get; init; }
    public string? PhaseId { get; init; }
    public string? MilestoneId { get; init; }
}

[ApiController]
[Route("api/v1/issues")]
public class IssuesController : ControllerBase
{
    private static readonly List<IssueSummary> _issues = [];

    /// <summary>
    /// Returns issues, optionally filtered by status, severity, taskId, phaseId, or milestoneId.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<IssueSummary>), StatusCodes.Status200OK)]
    public IActionResult GetAll(
        [FromQuery] string? status,
        [FromQuery] string? severity,
        [FromQuery] string? taskId,
        [FromQuery] string? phaseId,
        [FromQuery] string? milestoneId)
    {
        IEnumerable<IssueSummary> result = _issues;

        if (!string.IsNullOrEmpty(status))
            result = result.Where(i => i.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(severity))
            result = result.Where(i => i.Severity.Equals(severity, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrEmpty(taskId))
            result = result.Where(i => i.TaskId == taskId);

        if (!string.IsNullOrEmpty(phaseId))
            result = result.Where(i => i.PhaseId == phaseId);

        if (!string.IsNullOrEmpty(milestoneId))
            result = result.Where(i => i.MilestoneId == milestoneId);

        return Ok(result.ToList());
    }

    /// <summary>
    /// Returns a single issue by id.
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(IssueSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetById(string id)
    {
        var issue = _issues.FirstOrDefault(i => i.Id == id);
        return issue is null ? NotFound() : Ok(issue);
    }
}
