using System.Text.Json;

namespace Gmsd.Agents.Execution.Migration;

/// <summary>
/// Detects the format version of artifact files to determine if migration is needed.
/// </summary>
public static class ArtifactFormatDetector
{
    /// <summary>
    /// Detects the artifact type and format version from a JSON artifact.
    /// </summary>
    public static ArtifactFormatInfo DetectFormat(string artifactPath, string artifactJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(artifactJson);

        try
        {
            using var doc = JsonDocument.Parse(artifactJson);
            var root = doc.RootElement;

            // Check for schemaId field (new format indicator)
            if (root.TryGetProperty("schemaId", out var schemaIdElement))
            {
                var schemaId = schemaIdElement.GetString() ?? "";
                var version = ExtractSchemaVersion(schemaId);
                return new ArtifactFormatInfo
                {
                    ArtifactPath = artifactPath,
                    ArtifactType = DetermineArtifactTypeFromSchemaId(schemaId),
                    IsNewFormat = true,
                    SchemaVersion = version,
                    SchemaId = schemaId
                };
            }

            // Check for schemaVersion field (new format indicator)
            if (root.TryGetProperty("schemaVersion", out var schemaVersionElement))
            {
                var schemaVersion = schemaVersionElement.GetString() ?? "";
                var artifactType = DetermineArtifactTypeFromPath(artifactPath);
                return new ArtifactFormatInfo
                {
                    ArtifactPath = artifactPath,
                    ArtifactType = artifactType,
                    IsNewFormat = true,
                    SchemaVersion = ExtractSchemaVersion(schemaVersion),
                    SchemaId = BuildSchemaId(artifactType, ExtractSchemaVersion(schemaVersion))
                };
            }

            // Old format detection based on path and structure
            var detectedType = DetermineArtifactTypeFromPath(artifactPath);
            var isOldFormat = IsOldFormatStructure(root, detectedType);

            return new ArtifactFormatInfo
            {
                ArtifactPath = artifactPath,
                ArtifactType = detectedType,
                IsNewFormat = false,
                SchemaVersion = 0,
                SchemaId = null
            };
        }
        catch
        {
            return new ArtifactFormatInfo
            {
                ArtifactPath = artifactPath,
                ArtifactType = ArtifactType.Unknown,
                IsNewFormat = false,
                SchemaVersion = 0,
                SchemaId = null
            };
        }
    }

    /// <summary>
    /// Determines artifact type from the file path.
    /// </summary>
    private static ArtifactType DetermineArtifactTypeFromPath(string artifactPath)
    {
        var normalizedPath = artifactPath.Replace("\\", "/").ToLowerInvariant();

        if (normalizedPath.Contains("/.aos/spec/phases/") && normalizedPath.EndsWith("/plan.json"))
            return ArtifactType.PhasePlan;

        if (normalizedPath.Contains("/.aos/spec/tasks/") && normalizedPath.EndsWith("/plan.json"))
            return ArtifactType.TaskPlan;

        if (normalizedPath.Contains("/.aos/spec/uat/") && normalizedPath.EndsWith(".json"))
            return ArtifactType.VerifierInput;

        if (normalizedPath.Contains("/.aos/evidence/runs/") && normalizedPath.EndsWith("uat-results.json"))
            return ArtifactType.VerifierOutput;

        if (normalizedPath.Contains("/.aos/spec/fixes/") && normalizedPath.EndsWith("/plan.json"))
            return ArtifactType.FixPlan;

        if (normalizedPath.Contains("/.aos/diagnostics/") && normalizedPath.EndsWith(".diagnostic.json"))
            return ArtifactType.Diagnostic;

        return ArtifactType.Unknown;
    }

    /// <summary>
    /// Determines artifact type from schema ID.
    /// </summary>
    private static ArtifactType DetermineArtifactTypeFromSchemaId(string schemaId)
    {
        if (schemaId.Contains("phase-plan"))
            return ArtifactType.PhasePlan;
        if (schemaId.Contains("task-plan"))
            return ArtifactType.TaskPlan;
        if (schemaId.Contains("verifier-input"))
            return ArtifactType.VerifierInput;
        if (schemaId.Contains("verifier-output"))
            return ArtifactType.VerifierOutput;
        if (schemaId.Contains("fix-plan"))
            return ArtifactType.FixPlan;
        if (schemaId.Contains("diagnostic"))
            return ArtifactType.Diagnostic;

        return ArtifactType.Unknown;
    }

    /// <summary>
    /// Checks if the artifact structure matches old format patterns.
    /// </summary>
    private static bool IsOldFormatStructure(JsonElement root, ArtifactType artifactType)
    {
        return artifactType switch
        {
            ArtifactType.TaskPlan => !root.TryGetProperty("schemaVersion", out _) && 
                                     root.TryGetProperty("tasks", out _),
            ArtifactType.VerifierInput => !root.TryGetProperty("schemaVersion", out _) && 
                                          root.TryGetProperty("criteria", out _),
            ArtifactType.VerifierOutput => !root.TryGetProperty("schemaVersion", out _) && 
                                           root.TryGetProperty("checks", out _),
            ArtifactType.FixPlan => !root.TryGetProperty("schemaVersion", out _) && 
                                    root.TryGetProperty("fixes", out _),
            _ => false
        };
    }

    /// <summary>
    /// Extracts version number from schema ID or version string.
    /// </summary>
    private static int ExtractSchemaVersion(string schemaIdOrVersion)
    {
        var parts = schemaIdOrVersion.Split(':');
        var versionPart = parts.LastOrDefault() ?? "v1";
        
        if (versionPart.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(versionPart.Substring(1), out var version))
                return version;
        }

        if (int.TryParse(versionPart, out var directVersion))
            return directVersion;

        return 1;
    }

    /// <summary>
    /// Builds a canonical schema ID from artifact type and version.
    /// </summary>
    private static string BuildSchemaId(ArtifactType artifactType, int version)
    {
        var typeString = artifactType switch
        {
            ArtifactType.PhasePlan => "phase-plan",
            ArtifactType.TaskPlan => "task-plan",
            ArtifactType.VerifierInput => "verifier-input",
            ArtifactType.VerifierOutput => "verifier-output",
            ArtifactType.FixPlan => "fix-plan",
            ArtifactType.Diagnostic => "diagnostic",
            _ => "unknown"
        };

        return $"gmsd:aos:schema:{typeString}:v{version}";
    }
}

/// <summary>
/// Information about detected artifact format.
/// </summary>
public sealed record ArtifactFormatInfo
{
    /// <summary>
    /// Path to the artifact file.
    /// </summary>
    public required string ArtifactPath { get; init; }

    /// <summary>
    /// Detected artifact type.
    /// </summary>
    public required ArtifactType ArtifactType { get; init; }

    /// <summary>
    /// Whether the artifact is in new canonical format.
    /// </summary>
    public required bool IsNewFormat { get; init; }

    /// <summary>
    /// Schema version of the artifact.
    /// </summary>
    public required int SchemaVersion { get; init; }

    /// <summary>
    /// Schema ID if detected.
    /// </summary>
    public string? SchemaId { get; init; }

    /// <summary>
    /// Whether migration is needed.
    /// </summary>
    public bool RequiresMigration => !IsNewFormat && ArtifactType != ArtifactType.Unknown;
}

/// <summary>
/// Enumeration of artifact types.
/// </summary>
public enum ArtifactType
{
    Unknown = 0,
    PhasePlan = 1,
    TaskPlan = 2,
    VerifierInput = 3,
    VerifierOutput = 4,
    FixPlan = 5,
    Diagnostic = 6
}
