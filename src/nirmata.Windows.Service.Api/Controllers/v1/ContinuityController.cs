using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class WorkspacePosition
{
    public string MilestoneId { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string TaskId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed class StateSummary
{
    public WorkspacePosition Position { get; init; } = new();
    public IReadOnlyList<object> Decisions { get; init; } = [];
    public IReadOnlyList<object> Blockers { get; init; } = [];
}

public sealed class HandoffSummary
{
    public string? Cursor { get; init; }
    public string? InFlightTaskId { get; init; }
    public string? NextCommand { get; init; }
    public DateTime? WrittenAt { get; init; }
}

public sealed class EventSummary
{
    public string Type { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string? Payload { get; init; }
}

public sealed class ContextPackSummary
{
    public string PackId { get; init; } = string.Empty;
    public string Mode { get; init; } = string.Empty;
    public int ArtifactCount { get; init; }
}

public sealed class ContinuitySnapshot
{
    public StateSummary? State { get; init; }
    public HandoffSummary? Handoff { get; init; }
    public IReadOnlyList<EventSummary> RecentEvents { get; init; } = [];
    public IReadOnlyList<ContextPackSummary> ContextPacks { get; init; } = [];
}

[ApiController]
[Route("api/v1/continuity")]
public class ContinuityController : ControllerBase
{
    /// <summary>
    /// Returns the current continuity snapshot: state, handoff, recent events, and context packs.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ContinuitySnapshot), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        var snapshot = new ContinuitySnapshot
        {
            State = new StateSummary(),
            Handoff = null,
            RecentEvents = [],
            ContextPacks = []
        };

        return Ok(snapshot);
    }

    /// <summary>
    /// Returns only the current state cursor.
    /// </summary>
    [HttpGet("state")]
    [ProducesResponseType(typeof(StateSummary), StatusCodes.Status200OK)]
    public IActionResult GetState() => Ok(new StateSummary());

    /// <summary>
    /// Returns the current handoff snapshot, if one exists.
    /// </summary>
    [HttpGet("handoff")]
    [ProducesResponseType(typeof(HandoffSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetHandoff() => NotFound();

    /// <summary>
    /// Returns recent events from the event log, newest first.
    /// </summary>
    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<EventSummary>), StatusCodes.Status200OK)]
    public IActionResult GetEvents([FromQuery] int limit = 50) => Ok(Array.Empty<EventSummary>());

    /// <summary>
    /// Returns all context packs currently on disk.
    /// </summary>
    [HttpGet("packs")]
    [ProducesResponseType(typeof(IReadOnlyList<ContextPackSummary>), StatusCodes.Status200OK)]
    public IActionResult GetPacks() => Ok(Array.Empty<ContextPackSummary>());
}
