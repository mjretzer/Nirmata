using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.Workspaces;

/// <summary>
/// Request body for registering a new workspace.
/// </summary>
public sealed class WorkspaceCreateRequest
{
    /// <summary>Display name for the workspace.</summary>
    [Required]
    [MaxLength(200)]
    public required string Name { get; init; }

    /// <summary>Absolute path to the workspace root on disk.</summary>
    [Required]
    public required string Path { get; init; }
}
