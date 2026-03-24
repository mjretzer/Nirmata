using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class TaskSummary
{
    public string Id { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string MilestoneId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

[ApiController]
[Route("api/v1/tasks")]
public class TasksController : ControllerBase
{
    private static readonly List<TaskSummary> _tasks = [];

    /// <summary>
    /// Returns tasks, optionally filtered by phaseId, milestoneId, or status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaskSummary>), StatusCodes.Status200OK)]
    public IActionResult GetAll(
        [FromQuery] string? phaseId,
        [FromQuery] string? milestoneId,
        [FromQuery] string? status)
    {
        IEnumerable<TaskSummary> result = _tasks;

        if (!string.IsNullOrEmpty(phaseId))
            result = result.Where(t => t.PhaseId == phaseId);

        if (!string.IsNullOrEmpty(milestoneId))
            result = result.Where(t => t.MilestoneId == milestoneId);

        if (!string.IsNullOrEmpty(status))
            result = result.Where(t => t.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        return Ok(result.ToList());
    }
}
