using System.Text.Json;
using System.Text.Json.Serialization;

namespace nirmata.Data.Dto.Models.Codebase;

/// <summary>
/// Detail record for a single recognized codebase artifact, including its parsed payload.
/// Returned by <c>GET /v1/workspaces/{workspaceId}/codebase/{artifactId}</c>.
/// </summary>
public sealed class CodebaseArtifactDetailDto
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

    /// <summary>Workspace-relative path of the artifact file.</summary>
    public required string Path { get; init; }

    /// <summary>Last write time of the artifact file; <see langword="null"/> when the file is absent.</summary>
    public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>
    /// Parsed JSON payload of the artifact file.
    /// <see langword="null"/> when the file is absent (<c>missing</c>) or unreadable (<c>error</c>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Payload { get; init; }
}
