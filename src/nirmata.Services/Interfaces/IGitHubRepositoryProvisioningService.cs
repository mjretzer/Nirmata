namespace nirmata.Services.Interfaces;

/// <summary>
/// Creates or reuses a GitHub repository for a given authenticated user.
/// </summary>
public interface IGitHubRepositoryProvisioningService
{
    /// <summary>
    /// Ensures a GitHub repository exists for the given owner and name.
    /// Creates the repository if it does not exist; reuses it if it does.
    /// </summary>
    /// <param name="ownerLogin">GitHub login of the authenticated user.</param>
    /// <param name="repositoryName">Name of the repository to create or reuse.</param>
    /// <param name="isPrivate">Whether to create a private repository.</param>
    /// <param name="accessToken">GitHub access token obtained from the OAuth flow.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the clone URL and whether the repository was newly created.</returns>
    Task<GitHubRepositoryProvisionResult> EnsureRepositoryAsync(
        string ownerLogin,
        string repositoryName,
        bool isPrivate,
        string accessToken,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from <see cref="IGitHubRepositoryProvisioningService.EnsureRepositoryAsync"/>.
/// </summary>
/// <param name="CloneUrl">HTTPS clone URL for the repository.</param>
/// <param name="HtmlUrl">Browser URL for the repository on GitHub.</param>
/// <param name="Created"><c>true</c> when a new repository was created; <c>false</c> when an existing one was reused.</param>
public sealed record GitHubRepositoryProvisionResult(
    string CloneUrl,
    string HtmlUrl,
    bool Created);
