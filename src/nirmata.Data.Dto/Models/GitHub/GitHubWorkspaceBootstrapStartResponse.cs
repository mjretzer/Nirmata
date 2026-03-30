namespace nirmata.Data.Dto.Models.GitHub;

/// <summary>
/// Response returned when the GitHub OAuth bootstrap flow is started.
/// </summary>
public sealed class GitHubWorkspaceBootstrapStartResponse
{
    /// <summary>GitHub authorize URL the browser should open.</summary>
    public required string AuthorizeUrl { get; init; }
}
