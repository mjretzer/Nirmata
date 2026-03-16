namespace nirmata.Aos.Engine.Errors;

/// <summary>
/// Normalized error envelope for machine-readable failures.
/// </summary>
/// <param name="Code">Stable identifier for the error.</param>
/// <param name="Message">Human-readable, actionable message.</param>
/// <param name="Details">Optional structured context (e.g., contract paths, option names).</param>
internal sealed record AosErrorEnvelope(
    string Code,
    string Message,
    object? Details = null);

