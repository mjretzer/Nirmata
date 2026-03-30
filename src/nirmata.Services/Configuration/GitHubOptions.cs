namespace nirmata.Services.Configuration;

/// <summary>
/// Configuration for the GitHub OAuth application used for workspace bootstrap.
/// Set <c>GitHub:ClientId</c> and <c>GitHub:ClientSecret</c> via environment variables,
/// user secrets, or appsettings overrides. Never commit real credentials.
/// </summary>
public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";

    /// <summary>GitHub OAuth App client ID.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>GitHub OAuth App client secret. Keep this out of source control.</summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional explicit callback URL sent to GitHub during the OAuth flow.
    /// When empty, the callback URL is derived from the current HTTP request.
    /// Set this when the app is behind a reverse proxy or deployed to a known public URL.
    /// Example: https://app.example.com/v1/github/bootstrap/callback
    /// </summary>
    public string CallbackUrl { get; set; } = string.Empty;
}
