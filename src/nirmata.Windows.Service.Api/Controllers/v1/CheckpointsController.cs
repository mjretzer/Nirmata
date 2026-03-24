using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class CheckpointSummary
{
    public string Timestamp { get; init; } = string.Empty;
    public string MilestoneId { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
}

[ApiController]
[Route("api/v1/checkpoints")]
public class CheckpointsController : ControllerBase
{
    private static readonly List<CheckpointSummary> _checkpoints = [];

    /// <summary>
    /// Returns all checkpoint snapshots.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CheckpointSummary>), StatusCodes.Status200OK)]
    public IActionResult GetAll() => Ok(_checkpoints);

    /// <summary>
    /// Returns a single checkpoint by timestamp.
    /// </summary>
    [HttpGet("{timestamp}")]
    [ProducesResponseType(typeof(CheckpointSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetByTimestamp(string timestamp)
    {
        var checkpoint = _checkpoints.FirstOrDefault(c => c.Timestamp == timestamp);
        return checkpoint is null ? NotFound() : Ok(checkpoint);
    }
}
