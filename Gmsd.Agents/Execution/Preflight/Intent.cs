namespace Gmsd.Agents.Execution.Preflight;

public enum IntentKind
{
    SmallTalk,
    Explain,
    Help,
    Status,
    WorkflowCommand,
    WorkflowFreeform,
    Unknown
}

public enum SideEffect
{
    None,
    ReadOnly,
    Write
}

public sealed class Intent
{
    public required IntentKind Kind { get; init; }
    public required SideEffect SideEffect { get; init; }
    public required double Confidence { get; init; }
    public string? Command { get; init; }
    public string[]? Targets { get; init; }
}
