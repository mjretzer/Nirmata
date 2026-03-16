namespace nirmata.Agents.Execution.Preflight.CommandSuggestion;

/// <summary>
/// Represents a proposed command based on natural language input analysis.
/// </summary>
public sealed class CommandProposal
{
    /// <summary>
    /// The name of the suggested command (e.g., "run", "plan", "status").
    /// </summary>
    public required string CommandName { get; init; }

    /// <summary>
    /// Arguments to be passed to the command.
    /// </summary>
    public string[] Arguments { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Confidence score (0.0-1.0) indicating how certain the suggestion is.
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Reasoning or explanation for why this command was suggested.
    /// </summary>
    public string? Reasoning { get; init; }

    /// <summary>
    /// The fully formatted command string (e.g., "/run --verbose test-suite").
    /// </summary>
    public string? FormattedCommand { get; init; }

    /// <summary>
    /// Validates that the proposal meets all requirements.
    /// </summary>
    /// <returns>True if valid; false otherwise.</returns>
    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(CommandName))
            return false;

        if (Confidence < 0.0 || Confidence > 1.0)
            return false;

        return true;
    }

    /// <summary>
    /// Validates and returns a list of validation errors.
    /// </summary>
    /// <returns>List of error messages; empty if valid.</returns>
    public IReadOnlyList<string> GetValidationErrors()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CommandName))
            errors.Add("CommandName must be non-empty.");

        if (Confidence < 0.0 || Confidence > 1.0)
            errors.Add("Confidence must be between 0.0 and 1.0.");

        return errors;
    }
}
