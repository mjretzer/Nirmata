namespace Gmsd.Aos.Engine.Evidence.ExecutePlan;

internal sealed record ExecutePlanActionsLog(
    int SchemaVersion,
    IReadOnlyList<string> OutputsWritten);

