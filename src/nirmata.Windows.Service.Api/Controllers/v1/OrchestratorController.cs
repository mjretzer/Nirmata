using Microsoft.AspNetCore.Mvc;

namespace nirmata.Windows.Service.Api.Controllers.V1;

public sealed class GateCheck
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty; // "dependency" | "uat" | "evidence"
    public string Label { get; init; } = string.Empty;
    public string Detail { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty; // "pass" | "fail" | "warn"
    public string? ActionLabel { get; init; }
}

public sealed class NextTaskGate
{
    public string TaskId { get; init; } = string.Empty;
    public string TaskName { get; init; } = string.Empty;
    public string PhaseId { get; init; } = string.Empty;
    public string PhaseTitle { get; init; } = string.Empty;
    public bool Runnable { get; init; }
    public IReadOnlyList<GateCheck> Checks { get; init; } = [];
    public string RecommendedAction { get; init; } = string.Empty;
}

public sealed class OrchestratorTimelineStep
{
    public string Id { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty; // "pending" | "running" | "completed" | "failed"
}

public sealed class OrchestratorStateSnapshot
{
    public NextTaskGate? Gate { get; init; }
    public string Mode { get; init; } = string.Empty; // "chat" | "command" | "auto"
    public IReadOnlyList<OrchestratorTimelineStep> DefaultTimeline { get; init; } = [];
}

[ApiController]
[Route("api/v1/orchestrator")]
public class OrchestratorController : ControllerBase
{
    private static readonly IReadOnlyList<OrchestratorTimelineStep> _defaultTimeline =
    [
        new() { Id = "validate", Label = "Validate", Status = "pending" },
        new() { Id = "roadmap",  Label = "Roadmap",  Status = "pending" },
        new() { Id = "plan",     Label = "Plan",     Status = "pending" },
        new() { Id = "execute",  Label = "Execute",  Status = "pending" },
        new() { Id = "verify",   Label = "Verify",   Status = "pending" },
        new() { Id = "persist",  Label = "Persist",  Status = "pending" },
    ];

    /// <summary>
    /// Returns the current orchestrator state: gate checks, mode, and timeline template.
    /// </summary>
    [HttpGet("state")]
    [ProducesResponseType(typeof(OrchestratorStateSnapshot), StatusCodes.Status200OK)]
    public IActionResult GetState()
    {
        var snapshot = new OrchestratorStateSnapshot
        {
            Gate = null,
            Mode = "chat",
            DefaultTimeline = _defaultTimeline
        };

        return Ok(snapshot);
    }
}
