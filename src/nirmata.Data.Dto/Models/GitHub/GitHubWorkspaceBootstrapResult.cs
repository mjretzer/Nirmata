using nirmata.Data.Dto.Models.Workspaces;

namespace nirmata.Data.Dto.Models.GitHub;

/// <summary>
/// Result returned after completing a GitHub-connected workspace bootstrap flow.
/// </summary>
public sealed class GitHubWorkspaceBootstrapResult
{
    /// <summary>The workspace that was registered after bootstrap.</summary>
    public required WorkspaceSummary Workspace { get; init; }

    /// <summary>The GitHub account that authorized the repo creation.</summary>
    public required string Owner { get; init; }

    /// <summary>The repository name that was created or reused.</summary>
    public required string RepositoryName { get; init; }

    /// <summary>The remote URL configured for the local workspace.</summary>
    public required string RepositoryUrl { get; init; }

    /// <summary>The frontend origin that initiated the OAuth flow.</summary>
    public required string FrontendOrigin { get; init; }

    /// <summary><c>true</c> when GitHub created a new repository.</summary>
    public bool RepositoryCreated { get; init; }

    /// <summary><c>true</c> when the local workspace origin was configured during bootstrap.</summary>
    public bool OriginConfigured { get; init; }
}
