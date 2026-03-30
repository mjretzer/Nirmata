using System.ComponentModel.DataAnnotations;

namespace nirmata.Data.Dto.Requests.GitHub;

/// <summary>
/// Request body for starting a GitHub-connected workspace bootstrap flow.
/// </summary>
public sealed class GitHubWorkspaceBootstrapStartRequest
{
    /// <summary>Absolute path to the workspace root directory to bootstrap.</summary>
    [Required]
    public required string Path { get; init; }

    /// <summary>Display name used for the workspace registration.</summary>
    [Required]
    public required string Name { get; init; }

    /// <summary>Optional repository name to create on GitHub. Falls back to <see cref="Name"/> when omitted.</summary>
    public string? RepositoryName { get; init; }

    /// <summary>Whether the created GitHub repository should be private.</summary>
    public bool IsPrivate { get; init; } = true;
}
