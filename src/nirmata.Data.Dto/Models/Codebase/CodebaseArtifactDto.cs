namespace nirmata.Data.Dto.Models.Codebase;

/// <summary>
/// Summary record for a single recognized codebase artifact.
/// Returned as part of the workspace codebase inventory.
/// </summary>
public sealed class CodebaseArtifactDto
{
    /// <summary>Stable artifact identifier (e.g. "map", "stack", "architecture").</summary>
    public required string Id { get; init; }

    /// <summary>Artifact type — currently identical to <see cref="Id"/>.</summary>
    public required string Type { get; init; }

    /// <summary>
    /// Freshness status derived from file presence and manifest hash.
    /// One of <see cref="CodebaseArtifactStatus"/> constants.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Workspace-relative path of the artifact file (e.g. ".aos/codebase/map.json").</summary>
    public required string Path { get; init; }

    /// <summary>Last write time of the artifact file; <see langword="null"/> when the file is absent.</summary>
    public DateTimeOffset? LastUpdated { get; init; }
}
