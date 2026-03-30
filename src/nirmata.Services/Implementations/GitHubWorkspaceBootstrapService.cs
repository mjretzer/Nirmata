using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using nirmata.Common.Exceptions;
using nirmata.Data.Dto.Models.GitHub;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.GitHub;
using nirmata.Services.Configuration;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

public sealed class GitHubWorkspaceBootstrapService : IGitHubWorkspaceBootstrapService
{
    private const string GitHubAuthorizeEndpoint = "https://github.com/login/oauth/authorize";
    private const string GitHubAccessTokenEndpoint = "https://github.com/login/oauth/access_token";
    private const string GitHubApiBaseUrl = "https://api.github.com/";
    private const string ProtectorPurpose = "nirmata.github-workspace-bootstrap.state";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubOptions _githubOptions;
    private readonly IGitHubRepositoryProvisioningService _repositoryProvisioningService;
    private readonly IWorkspaceBootstrapService _workspaceBootstrapService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IDataProtector _stateProtector;
    private readonly ILogger<GitHubWorkspaceBootstrapService> _logger;

    public GitHubWorkspaceBootstrapService(
        IHttpClientFactory httpClientFactory,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<GitHubOptions> githubOptions,
        IGitHubRepositoryProvisioningService repositoryProvisioningService,
        IWorkspaceBootstrapService workspaceBootstrapService,
        IWorkspaceService workspaceService,
        ILogger<GitHubWorkspaceBootstrapService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _githubOptions = githubOptions.Value;
        _repositoryProvisioningService = repositoryProvisioningService;
        _workspaceBootstrapService = workspaceBootstrapService;
        _workspaceService = workspaceService;
        _stateProtector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
        _logger = logger;
    }

    public Task<string> CreateAuthorizeUrlAsync(
        GitHubWorkspaceBootstrapStartRequest request,
        string frontendOrigin,
        Uri callbackUri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(_githubOptions.ClientId))
            throw new ValidationFailedException("Missing required configuration value: GitHub:ClientId.");
        ValidateWorkspaceRequest(request);

        if (string.IsNullOrWhiteSpace(frontendOrigin))
            throw new ValidationFailedException("A frontend origin is required to return after GitHub authorization.");

        var state = new GitHubBootstrapState(
            NormalizePath(request.Path),
            request.Name.Trim(),
            NormalizeRepositoryName(request.RepositoryName ?? request.Name),
            request.IsPrivate,
            frontendOrigin.TrimEnd('/'));

        var protectedState = _stateProtector.Protect(JsonSerializer.Serialize(state));
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = _githubOptions.ClientId,
            ["redirect_uri"] = callbackUri.ToString(),
            ["scope"] = "repo read:user",
            ["allow_signup"] = "true",
            ["state"] = protectedState,
        };

        var authorizeUrl = QueryHelpers.AddQueryString(GitHubAuthorizeEndpoint, query);
        return Task.FromResult(authorizeUrl);
    }

    public async Task<GitHubWorkspaceBootstrapResult> CompleteAsync(
        string code,
        string protectedState,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ValidationFailedException("GitHub authorization code is missing.");

        if (string.IsNullOrWhiteSpace(protectedState))
            throw new ValidationFailedException("GitHub authorization state is missing.");

        var state = DeserializeState(protectedState);
        var token = await ExchangeCodeForTokenAsync(code, cancellationToken);
        var ownerLogin = await GetAuthenticatedUserLoginAsync(token, cancellationToken);
        var repository = await _repositoryProvisioningService.EnsureRepositoryAsync(
            ownerLogin, state.RepositoryName, state.IsPrivate, token, cancellationToken);

        var bootstrapResult = await _workspaceBootstrapService.BootstrapAsync(
            state.Path,
            cancellationToken,
            repository.CloneUrl);

        if (!bootstrapResult.Success)
        {
            _logger.LogError(
                "Local workspace bootstrap failed after GitHub repo provisioning for path '{Path}': {Error}",
                state.Path,
                bootstrapResult.Error ?? "Unknown error");

            throw new ValidationFailedException(
                $"GitHub repo was created, but local bootstrap failed: {bootstrapResult.Error ?? "Unknown error"}");
        }

        WorkspaceSummary workspace;
        try
        {
            workspace = await _workspaceService.RegisterAsync(state.Name, state.Path, cancellationToken);
        }
        catch (ValidationFailedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Workspace registration failed after GitHub bootstrap for workspace root '{Path}'",
                state.Path);

            throw new ValidationFailedException(
                $"GitHub repository bootstrap succeeded, but workspace registration failed: {ex.Message}");
        }

        return new GitHubWorkspaceBootstrapResult
        {
            Workspace = workspace,
            Owner = ownerLogin,
            RepositoryName = state.RepositoryName,
            RepositoryUrl = repository.CloneUrl,
            FrontendOrigin = state.FrontendOrigin,
            RepositoryCreated = repository.Created,
            OriginConfigured = bootstrapResult.OriginConfigured,
        };
    }

    public string GetFrontendOrigin(string protectedState)
    {
        var state = DeserializeState(protectedState);
        return state.FrontendOrigin;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static void ValidateWorkspaceRequest(GitHubWorkspaceBootstrapStartRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
            throw new ValidationFailedException("Workspace path is required.");

        if (!Path.IsPathRooted(request.Path))
            throw new ValidationFailedException("Workspace path must be an absolute path.");

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationFailedException("Workspace name is required.");
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new ValidationFailedException($"Workspace path is not valid: {ex.Message}");
        }
    }

    private static string NormalizeRepositoryName(string value)
    {
        var repo = value.Trim().ToLowerInvariant();
        repo = System.Text.RegularExpressions.Regex.Replace(repo, @"\s+", "-");
        repo = System.Text.RegularExpressions.Regex.Replace(repo, @"[^a-z0-9._-]", "");
        repo = System.Text.RegularExpressions.Regex.Replace(repo, @"-+", "-").Trim('-', '.', '_');

        if (string.IsNullOrWhiteSpace(repo))
            throw new ValidationFailedException("Repository name is required.");

        return repo;
    }

    private GitHubBootstrapState DeserializeState(string protectedState)
    {
        try
        {
            var json = _stateProtector.Unprotect(protectedState);
            var state = JsonSerializer.Deserialize<GitHubBootstrapState>(json);
            if (state is null)
                throw new ValidationFailedException("GitHub authorization state could not be read.");
            return state;
        }
        catch (Exception ex) when (ex is not ValidationFailedException)
        {
            throw new ValidationFailedException($"GitHub authorization state is invalid: {ex.Message}");
        }
    }

    private async Task<string> ExchangeCodeForTokenAsync(string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_githubOptions.ClientId))
            throw new ValidationFailedException("Missing required configuration value: GitHub:ClientId.");
        if (string.IsNullOrWhiteSpace(_githubOptions.ClientSecret))
            throw new ValidationFailedException("Missing required configuration value: GitHub:ClientSecret.");

        using var request = new HttpRequestMessage(HttpMethod.Post, GitHubAccessTokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _githubOptions.ClientId,
                ["client_secret"] = _githubOptions.ClientSecret,
                ["code"] = code,
            })
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("Nirmata");

        var client = _httpClientFactory.CreateClient(nameof(GitHubWorkspaceBootstrapService));
        var response = await client.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GitHubAccessTokenResponse>(cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode || payload is null)
        {
            var message = payload?.ErrorDescription ?? payload?.Error ?? $"GitHub token exchange failed with HTTP {(int)response.StatusCode}.";
            _logger.LogError("GitHub OAuth token exchange failed (HTTP {StatusCode}): {Message}", (int)response.StatusCode, message);
            throw new ValidationFailedException(message);
        }

        if (string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            _logger.LogError("GitHub OAuth token exchange succeeded but no access token was returned");
            throw new ValidationFailedException("GitHub did not return an access token.");
        }

        return payload.AccessToken;
    }

    private async Task<string> GetAuthenticatedUserLoginAsync(string accessToken, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(GitHubWorkspaceBootstrapService) + ".Api");
        client.BaseAddress = new Uri(GitHubApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Nirmata");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

        var response = await client.GetAsync("user", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GitHubUserResponse>(cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.Login))
        {
            _logger.LogError("GitHub user lookup failed (HTTP {StatusCode})", (int)response.StatusCode);
            throw new ValidationFailedException("GitHub account lookup failed after authorization.");
        }

        return payload.Login;
    }

    // ── Private types ─────────────────────────────────────────────────────────

    private sealed record GitHubBootstrapState(
        string Path,
        string Name,
        string RepositoryName,
        bool IsPrivate,
        string FrontendOrigin);

    private sealed class GitHubAccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private sealed class GitHubUserResponse
    {
        [JsonPropertyName("login")]
        public string Login { get; set; } = string.Empty;
    }
}
