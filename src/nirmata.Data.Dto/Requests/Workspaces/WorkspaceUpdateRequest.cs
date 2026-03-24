using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.Workspaces;

/// <summary>
/// Request body for updating a registered workspace's root path.
/// </summary>
public sealed class WorkspaceUpdateRequest
{
    /// <summary>New absolute path to the workspace root on disk.</summary>
    [Required]
    public required string Path { get; init; }
}
