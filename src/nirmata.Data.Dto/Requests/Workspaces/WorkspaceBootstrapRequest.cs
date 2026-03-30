using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.Workspaces;

/// <summary>
/// Request body for bootstrapping a workspace root.
/// Bootstrap creates or validates the git repository and seeds the AOS scaffold.
/// </summary>
public sealed class WorkspaceBootstrapRequest
{
    /// <summary>Absolute path to the workspace root directory to bootstrap.</summary>
    [Required]
    public required string Path { get; init; }
}
