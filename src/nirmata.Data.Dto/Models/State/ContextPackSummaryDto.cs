namespace nirmata.Data.Dto.Models.State;

public sealed class ContextPackSummaryDto
{
    /// <summary>Pack identifier, e.g. <c>TSK-000013</c> or <c>PH-0001</c>.</summary>
    public string PackId { get; init; } = string.Empty;

    /// <summary>Execution mode the pack was built for, e.g. <c>execute</c>, <c>plan</c>, <c>verify</c>.</summary>
    public string? Mode { get; init; }

    /// <summary>Token budget the pack was built against.</summary>
    public int? BudgetTokens { get; init; }

    /// <summary>Number of artifact entries in the pack.</summary>
    public int ArtifactCount { get; init; }
}
