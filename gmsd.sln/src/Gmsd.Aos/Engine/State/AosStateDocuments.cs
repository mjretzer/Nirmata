namespace Gmsd.Aos.Engine.State;

using System.Text.Json.Serialization;

/// <summary>
/// Shared, minimal state-layer documents written under <c>.aos/state/**</c>.
/// These are intentionally small and schema-light for early milestones.
/// </summary>
internal sealed record StateSnapshotDocument(int SchemaVersion, StateCursorDocument Cursor);

/// <summary>
/// Minimal workflow cursor (v2 shape).
/// All fields are optional; workspace validation constrains allowed status values.
/// </summary>
internal sealed record StateCursorDocument(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MilestoneId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PhaseId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TaskId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StepId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MilestoneStatus = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? PhaseStatus = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TaskStatus = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? StepStatus = null,
    // Legacy cursor reference (deprecated for operational cursoring)
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Kind = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Id = null
);

