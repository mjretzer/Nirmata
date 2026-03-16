using System;
using System.Collections.Generic;

namespace nirmata.Agents.Models.Runtime;

/// <summary>
/// Represents a canonical diagnostic artifact generated when validation fails.
/// </summary>
public sealed class DiagnosticArtifact
{
    /// <summary>
    /// Gets or sets the schema version of the diagnostic artifact itself.
    /// </summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// Gets or sets the unique schema identifier for the diagnostic artifact.
    /// </summary>
    public string SchemaId { get; set; } = "nirmata:aos:schema:diagnostic:v1";

    /// <summary>
    /// Gets or sets the path to the artifact that failed validation.
    /// </summary>
    public string ArtifactPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the identifier of the schema that failed validation.
    /// </summary>
    public string FailedSchemaId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version of the schema that failed validation.
    /// </summary>
    public int FailedSchemaVersion { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the diagnostic was generated.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the workflow phase where the failure occurred.
    /// </summary>
    public string Phase { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets additional context for the failure (e.g., taskId, runId).
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of validation errors found.
    /// </summary>
    public List<ValidationError> ValidationErrors { get; set; } = new();

    /// <summary>
    /// Gets or sets actionable repair suggestions for the identified errors.
    /// </summary>
    public List<string> RepairSuggestions { get; set; } = new();
}

/// <summary>
/// Represents a single validation error within a diagnostic artifact.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Gets or sets the JSON path or property path where the error occurred.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a human-readable error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expected value or structure.
    /// </summary>
    public string? Expected { get; set; }

    /// <summary>
    /// Gets or sets the actual value or structure found.
    /// </summary>
    public string? Actual { get; set; }
}
