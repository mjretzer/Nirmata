namespace nirmata.Data.Dto.Models.Codebase;

/// <summary>
/// Full workspace codebase intelligence inventory.
/// Returned by <c>GET /v1/workspaces/{workspaceId}/codebase</c>.
/// </summary>
public sealed class CodebaseInventoryDto
{
    /// <summary>Status record for every recognized codebase artifact.</summary>
    public required IReadOnlyList<CodebaseArtifactDto> Artifacts { get; init; }

    /// <summary>
    /// Language names detected in the workspace, derived from <c>stack.json</c>.
    /// Empty when <c>stack.json</c> is absent or unreadable.
    /// </summary>
    public required IReadOnlyList<string> Languages { get; init; }

    /// <summary>
    /// Framework and runtime names detected in the workspace, derived from <c>stack.json</c>.
    /// Empty when <c>stack.json</c> is absent or unreadable.
    /// </summary>
    public required IReadOnlyList<string> Stack { get; init; }
}
