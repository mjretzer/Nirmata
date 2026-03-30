using nirmata.Data.Dto.Models.GitHub;
using nirmata.Data.Dto.Requests.GitHub;

namespace nirmata.Services.Interfaces;

/// <summary>
/// Orchestrates the GitHub-connected bootstrap flow for a workspace.
/// </summary>
public interface IGitHubWorkspaceBootstrapService
{
    /// <summary>
    /// Builds the GitHub authorize URL for the bootstrap flow.
    /// </summary>
    Task<string> CreateAuthorizeUrlAsync(
        GitHubWorkspaceBootstrapStartRequest request,
        string frontendOrigin,
        Uri callbackUri,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the GitHub bootstrap flow after GitHub redirects back with an OAuth code.
    /// </summary>
    Task<GitHubWorkspaceBootstrapResult> CompleteAsync(
        string code,
        string protectedState,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodes the protected OAuth state and returns the frontend origin captured at start time.
    /// </summary>
    string GetFrontendOrigin(string protectedState);
}
