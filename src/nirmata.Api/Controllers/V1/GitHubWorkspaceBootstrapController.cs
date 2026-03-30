using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using nirmata.Common.Exceptions;
using nirmata.Data.Dto.Models.GitHub;
using nirmata.Data.Dto.Requests.GitHub;
using nirmata.Services.Configuration;
using nirmata.Services.Interfaces;

namespace nirmata.Api.Controllers.V1;

[Route("v1/github/bootstrap")]
public sealed class GitHubWorkspaceBootstrapController : nirmataController
{
    private readonly IGitHubWorkspaceBootstrapService _gitHubWorkspaceBootstrapService;
    private readonly GitHubOptions _githubOptions;

    public GitHubWorkspaceBootstrapController(
        IGitHubWorkspaceBootstrapService gitHubWorkspaceBootstrapService,
        IOptions<GitHubOptions> githubOptions)
    {
        _gitHubWorkspaceBootstrapService = gitHubWorkspaceBootstrapService;
        _githubOptions = githubOptions.Value;
    }

    /// <summary>
    /// Starts the GitHub OAuth flow for a GitHub-connected workspace bootstrap.
    /// </summary>
    [HttpPost("start")]
    [ProducesResponseType(typeof(GitHubWorkspaceBootstrapStartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartAsync(
        [FromBody] GitHubWorkspaceBootstrapStartRequest request,
        CancellationToken cancellationToken)
    {
        var frontendOrigin = Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(frontendOrigin))
            return BadRequestResult("GitHub bootstrap requires a browser origin so the app can return after authorization.");

        Uri callbackUri;
        try
        {
            callbackUri = ResolveCallbackUri();
        }
        catch (ValidationFailedException ex)
        {
            return BadRequestResult(ex.Message);
        }

        var authorizeUrl = await _gitHubWorkspaceBootstrapService.CreateAuthorizeUrlAsync(
            request,
            frontendOrigin,
            callbackUri,
            cancellationToken);

        return OkResult(new GitHubWorkspaceBootstrapStartResponse { AuthorizeUrl = authorizeUrl });
    }

    /// <summary>
    /// Completes the GitHub OAuth flow after GitHub redirects back to the app.
    /// </summary>
    [HttpGet("callback")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CallbackAsync(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery(Name = "error_description")] string? errorDescription,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(state))
            return BadRequestResult("GitHub callback is missing the OAuth state.");

        string frontendOrigin;
        try
        {
            frontendOrigin = _gitHubWorkspaceBootstrapService.GetFrontendOrigin(state);
        }
        catch (Exception ex)
        {
            return BadRequestResult(ex.Message);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            return Redirect(QueryHelpers.AddQueryString($"{frontendOrigin}/", new Dictionary<string, string?>
            {
                ["githubBootstrap"] = "error",
                ["message"] = string.IsNullOrWhiteSpace(errorDescription) ? error : errorDescription,
            }));
        }

        if (string.IsNullOrWhiteSpace(code))
            return Redirect(QueryHelpers.AddQueryString($"{frontendOrigin}/", new Dictionary<string, string?>
            {
                ["githubBootstrap"] = "error",
                ["message"] = "GitHub callback is missing the authorization code.",
            }));

        try
        {
            var result = await _gitHubWorkspaceBootstrapService.CompleteAsync(code, state, cancellationToken);
            return Redirect($"{result.FrontendOrigin}/ws/{result.Workspace.Id}");
        }
        catch (Exception ex)
        {
            return Redirect(QueryHelpers.AddQueryString($"{frontendOrigin}/", new Dictionary<string, string?>
            {
                ["githubBootstrap"] = "error",
                ["message"] = ex.Message,
            }));
        }
    }

    private Uri ResolveCallbackUri()
    {
        if (string.IsNullOrWhiteSpace(_githubOptions.CallbackUrl))
        {
            if (!string.Equals(Request.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                throw new ValidationFailedException("GitHub bootstrap requires the API to be served over HTTPS.");

            if (!Request.Host.HasValue)
                throw new ValidationFailedException("GitHub bootstrap requires a request host to build the callback URL.");

            return new UriBuilder(Request.Scheme, Request.Host.Host, Request.Host.Port ?? -1)
            {
                Path = $"{Request.PathBase}/v1/github/bootstrap/callback",
            }.Uri;
        }

        if (!Uri.TryCreate(_githubOptions.CallbackUrl, UriKind.Absolute, out var configuredCallbackUri))
            throw new ValidationFailedException("GitHub:CallbackUrl must be an absolute URL.");

        if (!string.Equals(configuredCallbackUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            throw new ValidationFailedException("GitHub:CallbackUrl must use HTTPS.");

        return configuredCallbackUri;
    }
}
