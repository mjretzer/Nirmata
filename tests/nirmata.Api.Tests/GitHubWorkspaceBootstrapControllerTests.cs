using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using nirmata.Api.Controllers.V1;
using nirmata.Data.Dto.Models.GitHub;
using nirmata.Data.Dto.Requests.GitHub;
using nirmata.Services.Configuration;
using nirmata.Services.Interfaces;
using Xunit;

namespace nirmata.Api.Tests;

public sealed class GitHubWorkspaceBootstrapControllerTests
{
    [Fact]
    public async Task StartAsync_UsesSecureBrowserOriginForCallbackUrl()
    {
        var service = new FakeGitHubWorkspaceBootstrapService
        {
            AuthorizeUrl = "https://github.com/login/oauth/authorize?client_id=client-id"
        };
        var controller = CreateController(service);

        var result = await controller.StartAsync(new GitHubWorkspaceBootstrapStartRequest
        {
            Path = @"C:\Workspaces\repo",
            Name = "My Workspace",
            IsPrivate = true,
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GitHubWorkspaceBootstrapStartResponse>(ok.Value);
        Assert.Equal(service.AuthorizeUrl, response.AuthorizeUrl);
        Assert.Equal(new Uri("https://localhost:7138/v1/github/bootstrap/callback"), service.CapturedCallbackUri);
        Assert.Equal("https://localhost:8443", service.CapturedFrontendOrigin);
    }

    [Fact]
    public async Task StartAsync_UsesConfiguredCallbackUrlWhenProvided()
    {
        var controller = CreateController(
            new FakeGitHubWorkspaceBootstrapService(),
            callbackUrl: "https://example.com/v1/github/bootstrap/callback");

        var result = await controller.StartAsync(new GitHubWorkspaceBootstrapStartRequest
        {
            Path = @"C:\Workspaces\repo",
            Name = "My Workspace",
            IsPrivate = true,
        }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<GitHubWorkspaceBootstrapStartResponse>(ok.Value);
        Assert.Equal("https://github.com/login/oauth/authorize?client_id=client-id", response.AuthorizeUrl);
    }

    [Fact]
    public async Task CallbackAsync_RedirectsToTheNewWorkspacePageAfterCompletion()
    {
        var service = new FakeGitHubWorkspaceBootstrapService
        {
            CompleteResult = new GitHubWorkspaceBootstrapResult
            {
                Workspace = new WorkspaceSummary
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Fresh Workspace",
                    Path = @"C:\Workspaces\fresh-workspace",
                    Status = "ready",
                    LastModified = DateTimeOffset.Parse("2026-03-26T19:00:00Z"),
                },
                Owner = "octo-cat",
                RepositoryName = "fresh-workspace",
                RepositoryUrl = "https://github.com/octo-cat/fresh-workspace.git",
                FrontendOrigin = "https://localhost:8443",
                RepositoryCreated = true,
                OriginConfigured = true,
            },
        };

        var controller = CreateController(service);

        var result = await controller.CallbackAsync(
            code: "auth-code",
            state: "protected-state",
            error: null,
            errorDescription: null,
            CancellationToken.None);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("https://localhost:8443/ws/11111111-1111-1111-1111-111111111111", redirect.Url);
    }

    private static GitHubWorkspaceBootstrapController CreateController(
        FakeGitHubWorkspaceBootstrapService service,
        string? callbackUrl = null)
    {
        var controller = new GitHubWorkspaceBootstrapController(
            service,
            Options.Create(new GitHubOptions
            {
                ClientId = "client-id",
                ClientSecret = "client-secret",
                CallbackUrl = callbackUrl ?? string.Empty,
            }))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            }
        };

        controller.HttpContext.Request.Headers.Origin = "https://localhost:8443";
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("localhost", 7138);
        return controller;
    }

    private sealed class FakeGitHubWorkspaceBootstrapService : IGitHubWorkspaceBootstrapService
    {
        public string AuthorizeUrl { get; init; } = "https://github.com/login/oauth/authorize";
        public string? CapturedFrontendOrigin { get; private set; }
        public Uri? CapturedCallbackUri { get; private set; }
        public GitHubWorkspaceBootstrapResult? CompleteResult { get; init; }

        public Task<string> CreateAuthorizeUrlAsync(
            GitHubWorkspaceBootstrapStartRequest request,
            string frontendOrigin,
            Uri callbackUri,
            CancellationToken cancellationToken = default)
        {
            CapturedFrontendOrigin = frontendOrigin;
            CapturedCallbackUri = callbackUri;
            return Task.FromResult(AuthorizeUrl);
        }

        public Task<GitHubWorkspaceBootstrapResult> CompleteAsync(
            string code,
            string protectedState,
            CancellationToken cancellationToken = default)
            => CompleteResult is not null
                ? Task.FromResult(CompleteResult)
                : throw new NotImplementedException();

        public string GetFrontendOrigin(string protectedState)
            => CapturedFrontendOrigin ?? throw new NotImplementedException();
    }
}
