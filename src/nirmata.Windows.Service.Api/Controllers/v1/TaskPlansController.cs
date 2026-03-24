using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class TaskPlanSummary
{
    public string TaskId { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string MilestoneId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int StepCount { get; init; }
}

[ApiController]
[Route("api/v1/task-plans")]
public class TaskPlansController : ControllerBase
{
    private static readonly List<TaskPlanSummary> _plans = [];

    /// <summary>
    /// Returns task plans, optionally filtered by taskId or phaseId.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TaskPlanSummary>), StatusCodes.Status200OK)]
    public IActionResult GetAll(
        [FromQuery] string? taskId,
        [FromQuery] string? phaseId)
    {
        IEnumerable<TaskPlanSummary> result = _plans;

        if (!string.IsNullOrEmpty(taskId))
            result = result.Where(p => p.TaskId == taskId);

        if (!string.IsNullOrEmpty(phaseId))
            result = result.Where(p => p.PhaseId == phaseId);

        return Ok(result.ToList());
    }
}
