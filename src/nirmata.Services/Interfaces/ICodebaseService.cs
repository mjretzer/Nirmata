using nirmata.Data.Dto.Models.Codebase;

namespace nirmata.Services.Interfaces;

/// <summary>
/// Reads workspace codebase intelligence artifacts from <c>.aos/codebase/</c> and exposes
/// artifact inventory, freshness classification, and parsed payloads.
/// </summary>
public interface ICodebaseService
{
    /// <summary>
    /// Returns the full codebase artifact inventory for the workspace, including language
    /// breakdown and stack metadata derived from <c>stack.json</c>.
    /// </summary>
    /// <param name="workspaceRoot">Absolute path to the workspace root directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CodebaseInventoryDto> GetInventoryAsync(
        string workspaceRoot, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the detail record and parsed payload for a recognized codebase artifact.
    /// Returns <see langword="null"/> when <paramref name="artifactId"/> is not a recognized artifact id.
    /// </summary>
    /// <param name="workspaceRoot">Absolute path to the workspace root directory.</param>
    /// <param name="artifactId">Artifact identifier (e.g. "map", "stack", "architecture").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<CodebaseArtifactDetailDto?> GetArtifactAsync(
        string workspaceRoot, string artifactId, CancellationToken cancellationToken = default);
}
