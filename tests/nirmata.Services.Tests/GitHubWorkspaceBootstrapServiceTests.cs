using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using nirmata.Common.Exceptions;
using nirmata.Data.Dto.Models.GitHub;
using nirmata.Data.Dto.Models.Workspaces;
using nirmata.Data.Dto.Requests.GitHub;
using nirmata.Services.Configuration;
using nirmata.Services.Implementations;
using nirmata.Services.Interfaces;
using Xunit;

namespace nirmata.Services.Tests;

public sealed class GitHubWorkspaceBootstrapServiceTests
{
    private readonly RoutingHttpMessageHandler _httpHandler = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly Mock<IGitHubRepositoryProvisioningService> _repositoryProvisioningService = new();
    private readonly Mock<IWorkspaceBootstrapService> _workspaceBootstrapService = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly GitHubWorkspaceBootstrapService _sut;

    public GitHubWorkspaceBootstrapServiceTests()
    {
        _httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_httpHandler, disposeHandler: false));

        _sut = new GitHubWorkspaceBootstrapService(
            _httpClientFactory.Object,
            new IdentityDataProtectionProvider(),
            Options.Create(new GitHubOptions
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                CallbackUrl = "https://app.example.com/v1/github/bootstrap/callback",
            }),
            _repositoryProvisioningService.Object,
            _workspaceBootstrapService.Object,
            _workspaceService.Object,
            NullLogger<GitHubWorkspaceBootstrapService>.Instance);
    }

    [Fact]
    public async Task CreateAuthorizeUrlAsync_PreservesWorkspaceContextAndFrontendOrigin()
    {
        var request = new GitHubWorkspaceBootstrapStartRequest
        {
            Path = Path.Combine(Path.GetTempPath(), $"nirm-github-init-{Guid.NewGuid():N}"),
            Name = "My App",
            RepositoryName = null,
            IsPrivate = true,
        };

        var authorizeUrl = await _sut.CreateAuthorizeUrlAsync(
            request,
            "https://localhost:8443/",
            new Uri("https://app.example.com/v1/github/bootstrap/callback"));

        var query = QueryHelpers.ParseQuery(new Uri(authorizeUrl).Query);
        var protectedState = query["state"].ToString();

        Assert.Equal("client-id", query["client_id"]);
        Assert.Equal("https://app.example.com/v1/github/bootstrap/callback", query["redirect_uri"]);
        Assert.Contains("repo", query["scope"].ToString());
        Assert.Contains("read:user", query["scope"].ToString());
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
        Assert.Equal("https://localhost:8443", _sut.GetFrontendOrigin(protectedState));
    }

    [Fact]
    public async Task CompleteAsync_WhenOAuthStateIsTampered_ThrowsValidationFailedException()
    {
        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _sut.CompleteAsync("auth-code", "definitely-not-valid-state"));

        Assert.Contains("GitHub authorization state", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_WhenRepositoryIsProvisioned_RegistersWorkspaceAndReturnsBootstrapResult()
    {
        var request = new GitHubWorkspaceBootstrapStartRequest
        {
            Path = Path.Combine(Path.GetTempPath(), $"nirm-github-flow-{Guid.NewGuid():N}"),
            Name = "My App",
            IsPrivate = true,
        };

        _httpHandler.Responder = async (message, cancellationToken) =>
        {
            if (message.Method == HttpMethod.Post && message.RequestUri?.AbsoluteUri == "https://github.com/login/oauth/access_token")
            {
                return JsonResponse(new
                {
                    access_token = "token-abc",
                    token_type = "bearer",
                    scope = "repo read:user",
                });
            }

            if (message.Method == HttpMethod.Get && message.RequestUri?.AbsoluteUri == "https://api.github.com/user")
            {
                return JsonResponse(new { login = "octo-cat" });
            }

            throw new InvalidOperationException($"Unexpected request: {message.Method} {message.RequestUri}");
        };

        _repositoryProvisioningService
            .Setup(service => service.EnsureRepositoryAsync(
                "octo-cat",
                "my-app",
                true,
                "token-abc",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubRepositoryProvisionResult(
                "https://github.com/octo-cat/my-app.git",
                "https://github.com/octo-cat/my-app",
                true));

        _workspaceBootstrapService
            .Setup(service => service.BootstrapAsync(
                request.Path,
                It.IsAny<CancellationToken>(),
                "https://github.com/octo-cat/my-app.git"))
            .ReturnsAsync(new WorkspaceBootstrapResult
            {
                Success = true,
                GitRepositoryCreated = false,
                AosScaffoldCreated = true,
                OriginConfigured = true,
                FailureKind = BootstrapFailureKind.None,
            });

        var expectedWorkspace = new WorkspaceSummary
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = request.Name,
            Path = request.Path,
            Status = "ready",
            LastModified = DateTimeOffset.Parse("2026-03-26T15:00:00Z"),
        };

        _workspaceService
            .Setup(service => service.RegisterAsync(request.Name, request.Path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedWorkspace);

        var state = await ExtractStateAsync(request);

        var result = await _sut.CompleteAsync("auth-code", state);

        Assert.Equal(expectedWorkspace.Id, result.Workspace.Id);
        Assert.Equal(expectedWorkspace.Name, result.Workspace.Name);
        Assert.Equal(expectedWorkspace.Path, result.Workspace.Path);
        Assert.Equal(expectedWorkspace.Status, result.Workspace.Status);
        Assert.Equal(expectedWorkspace.LastModified, result.Workspace.LastModified);
        Assert.Equal("octo-cat", result.Owner);
        Assert.Equal("my-app", result.RepositoryName);
        Assert.Equal("https://github.com/octo-cat/my-app.git", result.RepositoryUrl);
        Assert.Equal("https://localhost:8443", result.FrontendOrigin);
        Assert.True(result.RepositoryCreated);
        Assert.True(result.OriginConfigured);

        _repositoryProvisioningService.VerifyAll();
        _workspaceBootstrapService.VerifyAll();
        _workspaceService.VerifyAll();
    }

    [Fact]
    public async Task CompleteAsync_WhenRepositoryProvisioningReportsReuse_PropagatesReuseState()
    {
        var request = new GitHubWorkspaceBootstrapStartRequest
        {
            Path = Path.Combine(Path.GetTempPath(), $"nirm-github-reuse-{Guid.NewGuid():N}"),
            Name = "My App",
            IsPrivate = true,
        };

        _httpHandler.Responder = (message, cancellationToken) => message.Method switch
        {
            var method when method == HttpMethod.Post => Task.FromResult(JsonResponse(new { access_token = "token-abc" })),
            var method when method == HttpMethod.Get => Task.FromResult(JsonResponse(new { login = "octo-cat" })),
            _ => Task.FromException<HttpResponseMessage>(new InvalidOperationException($"Unexpected request: {message.Method} {message.RequestUri}")),
        };

        _repositoryProvisioningService
            .Setup(service => service.EnsureRepositoryAsync(
                "octo-cat",
                "my-app",
                true,
                "token-abc",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubRepositoryProvisionResult(
                "https://github.com/octo-cat/my-app.git",
                "https://github.com/octo-cat/my-app",
                false));

        _workspaceBootstrapService
            .Setup(service => service.BootstrapAsync(
                request.Path,
                It.IsAny<CancellationToken>(),
                "https://github.com/octo-cat/my-app.git"))
            .ReturnsAsync(new WorkspaceBootstrapResult
            {
                Success = true,
                GitRepositoryCreated = false,
                AosScaffoldCreated = false,
                OriginConfigured = true,
                FailureKind = BootstrapFailureKind.None,
            });

        _workspaceService
            .Setup(service => service.RegisterAsync(request.Name, request.Path, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceSummary
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = request.Name,
                Path = request.Path,
                Status = "ready",
                LastModified = DateTimeOffset.UtcNow,
            });

        var state = await ExtractStateAsync(request);

        var result = await _sut.CompleteAsync("auth-code", state);

        Assert.False(result.RepositoryCreated);
        Assert.Equal("https://github.com/octo-cat/my-app.git", result.RepositoryUrl);
    }

    [Fact]
    public async Task CompleteAsync_WhenLocalBootstrapFails_ThrowsValidationFailedException()
    {
        var request = new GitHubWorkspaceBootstrapStartRequest
        {
            Path = Path.Combine(Path.GetTempPath(), $"nirm-github-failure-{Guid.NewGuid():N}"),
            Name = "My App",
            IsPrivate = true,
        };

        _httpHandler.Responder = (message, cancellationToken) => message.Method switch
        {
            var method when method == HttpMethod.Post => Task.FromResult(JsonResponse(new { access_token = "token-abc" })),
            var method when method == HttpMethod.Get => Task.FromResult(JsonResponse(new { login = "octo-cat" })),
            _ => Task.FromException<HttpResponseMessage>(new InvalidOperationException($"Unexpected request: {message.Method} {message.RequestUri}")),
        };

        _repositoryProvisioningService
            .Setup(service => service.EnsureRepositoryAsync(
                "octo-cat",
                "my-app",
                true,
                "token-abc",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubRepositoryProvisionResult(
                "https://github.com/octo-cat/my-app.git",
                "https://github.com/octo-cat/my-app",
                true));

        _workspaceBootstrapService
            .Setup(service => service.BootstrapAsync(
                request.Path,
                It.IsAny<CancellationToken>(),
                "https://github.com/octo-cat/my-app.git"))
            .ReturnsAsync(new WorkspaceBootstrapResult
            {
                Success = false,
                Error = "origin setup failed",
                FailureKind = BootstrapFailureKind.GitCommandFailed,
            });

        var state = await ExtractStateAsync(request);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _sut.CompleteAsync("auth-code", state));

        Assert.Contains("local bootstrap failed", exception.Message);
        Assert.Contains("origin setup failed", exception.Message);
    }

    [Fact]
    public async Task CompleteAsync_WhenWorkspaceRegistrationFails_ThrowsValidationFailedException()
    {
        var request = new GitHubWorkspaceBootstrapStartRequest
        {
            Path = Path.Combine(Path.GetTempPath(), $"nirm-github-register-{Guid.NewGuid():N}"),
            Name = "My App",
            IsPrivate = true,
        };

        _httpHandler.Responder = (message, cancellationToken) => message.Method switch
        {
            var method when method == HttpMethod.Post => Task.FromResult(JsonResponse(new { access_token = "token-abc" })),
            var method when method == HttpMethod.Get => Task.FromResult(JsonResponse(new { login = "octo-cat" })),
            _ => Task.FromException<HttpResponseMessage>(new InvalidOperationException($"Unexpected request: {message.Method} {message.RequestUri}")),
        };

        _repositoryProvisioningService
            .Setup(service => service.EnsureRepositoryAsync(
                "octo-cat",
                "my-app",
                true,
                "token-abc",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubRepositoryProvisionResult(
                "https://github.com/octo-cat/my-app.git",
                "https://github.com/octo-cat/my-app",
                true));

        _workspaceBootstrapService
            .Setup(service => service.BootstrapAsync(
                request.Path,
                It.IsAny<CancellationToken>(),
                "https://github.com/octo-cat/my-app.git"))
            .ReturnsAsync(new WorkspaceBootstrapResult
            {
                Success = true,
                GitRepositoryCreated = false,
                AosScaffoldCreated = false,
                OriginConfigured = true,
                FailureKind = BootstrapFailureKind.None,
            });

        _workspaceService
            .Setup(service => service.RegisterAsync(request.Name, request.Path, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("workspace registry unavailable"));

        var state = await ExtractStateAsync(request);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _sut.CompleteAsync("auth-code", state));

        Assert.Contains("workspace registration failed", exception.Message);
        Assert.Contains("workspace registry unavailable", exception.Message);
    }

    private async Task<string> ExtractStateAsync(GitHubWorkspaceBootstrapStartRequest request)
    {
        var authorizeUrl = await _sut.CreateAuthorizeUrlAsync(
            request,
            "https://localhost:8443/",
            new Uri("https://app.example.com/v1/github/bootstrap/callback"));

        return QueryHelpers.ParseQuery(new Uri(authorizeUrl).Query)["state"].ToString();
    }

    private static HttpResponseMessage JsonResponse(object value, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }
}

public sealed class GitHubRepositoryProvisioningServiceTests
{
    private readonly RoutingHttpMessageHandler _httpHandler = new();
    private readonly Mock<IHttpClientFactory> _httpClientFactory = new();
    private readonly GitHubRepositoryProvisioningService _sut;

    public GitHubRepositoryProvisioningServiceTests()
    {
        _httpClientFactory
            .Setup(factory => factory.CreateClient(It.IsAny<string>()))
            .Returns(() => new HttpClient(_httpHandler, disposeHandler: false));

        _sut = new GitHubRepositoryProvisioningService(
            _httpClientFactory.Object,
            NullLogger<GitHubRepositoryProvisioningService>.Instance);
    }

    [Fact]
    public async Task EnsureRepositoryAsync_WhenRepositoryIsCreated_ReturnsCreatedResult()
    {
        _httpHandler.Responder = async (message, cancellationToken) =>
        {
            Assert.Equal(HttpMethod.Post, message.Method);
            Assert.Equal("https://api.github.com/user/repos", message.RequestUri?.AbsoluteUri);

            var body = await JsonDocument.ParseAsync(await message.Content!.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            Assert.Equal("my-app", body.RootElement.GetProperty("name").GetString());
            Assert.True(body.RootElement.GetProperty("private").GetBoolean());

            return JsonResponse(new
            {
                clone_url = "https://github.com/octo-cat/my-app.git",
                html_url = "https://github.com/octo-cat/my-app",
            }, HttpStatusCode.Created);
        };

        var result = await _sut.EnsureRepositoryAsync(
            "octo-cat",
            "my-app",
            true,
            "token-abc");

        Assert.True(result.Created);
        Assert.Equal("https://github.com/octo-cat/my-app.git", result.CloneUrl);
        Assert.Equal("https://github.com/octo-cat/my-app", result.HtmlUrl);
    }

    [Fact]
    public async Task EnsureRepositoryAsync_WhenRepositoryAlreadyExists_ReusesExistingRepository()
    {
        var requests = new List<(HttpMethod Method, string? Uri)>();

        _httpHandler.Responder = async (message, cancellationToken) =>
        {
            requests.Add((message.Method, message.RequestUri?.AbsoluteUri));

            if (message.Method == HttpMethod.Post)
            {
                return JsonResponse(new { message = "name already exists" }, HttpStatusCode.UnprocessableEntity);
            }

            if (message.Method == HttpMethod.Get)
            {
                Assert.Equal("https://api.github.com/repos/octo-cat/my-app", message.RequestUri?.AbsoluteUri);
                return JsonResponse(new
                {
                    clone_url = "https://github.com/octo-cat/my-app.git",
                    html_url = "https://github.com/octo-cat/my-app",
                });
            }

            throw new InvalidOperationException($"Unexpected request: {message.Method} {message.RequestUri}");
        };

        var result = await _sut.EnsureRepositoryAsync(
            "octo-cat",
            "my-app",
            true,
            "token-abc");

        Assert.False(result.Created);
        Assert.Equal("https://github.com/octo-cat/my-app.git", result.CloneUrl);
        Assert.Equal("https://github.com/octo-cat/my-app", result.HtmlUrl);
        Assert.Collection(requests,
            first => Assert.Equal(HttpMethod.Post, first.Method),
            second => Assert.Equal(HttpMethod.Get, second.Method));
    }

    [Fact]
    public async Task EnsureRepositoryAsync_WhenRepositoryLookupFails_ThrowsValidationFailedException()
    {
        _httpHandler.Responder = (message, cancellationToken) => message.Method switch
        {
            var method when method == HttpMethod.Post => Task.FromResult(JsonResponse(new { message = "name already exists" }, HttpStatusCode.UnprocessableEntity)),
            var method when method == HttpMethod.Get => Task.FromResult(JsonResponse(new { message = "not found" }, HttpStatusCode.NotFound)),
            _ => Task.FromException<HttpResponseMessage>(new InvalidOperationException($"Unexpected request: {message.Method} {message.RequestUri}")),
        };

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _sut.EnsureRepositoryAsync(
                "octo-cat",
                "my-app",
                true,
                "token-abc"));

        Assert.Contains("could not be read back", exception.Message);
    }

    [Fact]
    public async Task EnsureRepositoryAsync_WhenCreateReturnsError_ThrowsValidationFailedException()
    {
        _httpHandler.Responder = (message, cancellationToken) => Task.FromResult(
            JsonResponse(new { message = "API rate limit exceeded" }, HttpStatusCode.Forbidden));

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() =>
            _sut.EnsureRepositoryAsync(
                "octo-cat",
                "my-app",
                true,
                "token-abc"));

        Assert.Contains("API rate limit exceeded", exception.Message);
    }

    private static HttpResponseMessage JsonResponse(object value, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }
}

internal sealed class IdentityDataProtectionProvider : IDataProtectionProvider, IDataProtector
{
    public IDataProtector CreateProtector(string purpose) => this;

    public byte[] Protect(byte[] plaintext) => plaintext;

    public byte[] Unprotect(byte[] protectedData) => protectedData;
}

internal sealed class RoutingHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Responder { get; set; }
        = (_, _) => Task.FromException<HttpResponseMessage>(new InvalidOperationException("No HTTP response configured for this test."));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Responder(request, cancellationToken);
}
