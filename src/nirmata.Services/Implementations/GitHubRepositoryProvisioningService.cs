using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using nirmata.Common.Exceptions;
using nirmata.Services.Interfaces;

namespace nirmata.Services.Implementations;

public sealed class GitHubRepositoryProvisioningService : IGitHubRepositoryProvisioningService
{
    private const string GitHubApiBaseUrl = "https://api.github.com/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GitHubRepositoryProvisioningService> _logger;

    public GitHubRepositoryProvisioningService(
        IHttpClientFactory httpClientFactory,
        ILogger<GitHubRepositoryProvisioningService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<GitHubRepositoryProvisionResult> EnsureRepositoryAsync(
        string ownerLogin,
        string repositoryName,
        bool isPrivate,
        string accessToken,
        CancellationToken cancellationToken = default)
    {
        var client = CreateApiClient(accessToken);
        var createRequest = new CreateRepositoryRequest(repositoryName, isPrivate);

        var response = await client.PostAsJsonAsync("user/repos", createRequest, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var created = await response.Content.ReadFromJsonAsync<RepositoryResponse>(cancellationToken: cancellationToken);
            if (created is null || string.IsNullOrWhiteSpace(created.CloneUrl))
                throw new ValidationFailedException("GitHub created a repository but did not return its clone URL.");

            _logger.LogInformation("Created GitHub repository '{Owner}/{Repo}'", ownerLogin, repositoryName);
            return new GitHubRepositoryProvisionResult(created.CloneUrl, created.HtmlUrl, Created: true);
        }

        if ((int)response.StatusCode == 422)
        {
            _logger.LogInformation("GitHub repository '{Owner}/{Repo}' already exists; reusing it", ownerLogin, repositoryName);
            return await GetExistingRepositoryAsync(client, ownerLogin, repositoryName, cancellationToken);
        }

        var error = await ReadErrorMessageAsync(response, cancellationToken);
        _logger.LogError(
            "GitHub repository creation failed for '{Owner}/{Repo}' (HTTP {StatusCode}): {Error}",
            ownerLogin, repositoryName, (int)response.StatusCode, error);
        throw new ValidationFailedException($"GitHub repository creation failed: {error}");
    }

    private async Task<GitHubRepositoryProvisionResult> GetExistingRepositoryAsync(
        HttpClient client,
        string ownerLogin,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        var url = $"repos/{Uri.EscapeDataString(ownerLogin)}/{Uri.EscapeDataString(repositoryName)}";
        var response = await client.GetAsync(url, cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<RepositoryResponse>(cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode || payload is null || string.IsNullOrWhiteSpace(payload.CloneUrl))
        {
            _logger.LogError(
                "Failed to read back existing GitHub repository '{Owner}/{Repo}' (HTTP {StatusCode})",
                ownerLogin, repositoryName, (int)response.StatusCode);
            throw new ValidationFailedException("GitHub repository already exists but could not be read back.");
        }

        return new GitHubRepositoryProvisionResult(payload.CloneUrl, payload.HtmlUrl, Created: false);
    }

    private HttpClient CreateApiClient(string accessToken)
    {
        var client = _httpClientFactory.CreateClient(nameof(GitHubRepositoryProvisioningService));
        client.BaseAddress = new Uri(GitHubApiBaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Nirmata");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var payload = await response.Content.ReadFromJsonAsync<ErrorResponse>(cancellationToken: cancellationToken);
            return payload?.Message ?? response.ReasonPhrase ?? "Unknown GitHub error";
        }
        catch
        {
            return response.ReasonPhrase ?? "Unknown GitHub error";
        }
    }

    private sealed class CreateRepositoryRequest
    {
        [JsonPropertyName("name")]
        public string Name { get; init; }

        [JsonPropertyName("private")]
        public bool Private { get; init; }

        public CreateRepositoryRequest(string name, bool @private)
        {
            Name = name;
            Private = @private;
        }
    }

    private sealed class RepositoryResponse
    {
        [JsonPropertyName("clone_url")]
        public string CloneUrl { get; set; } = string.Empty;

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
    }

    private sealed class ErrorResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
