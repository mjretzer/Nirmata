using Gmsd.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace Gmsd.Web.Controllers;

/// <summary>
/// API controller for managing user feature flag preferences.
/// </summary>
[Route("api/feature-flags")]
[ApiController]
public class FeatureFlagsController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly ILogger<FeatureFlagsController> _logger;

    // Cookie name for persisting user preference across sessions
    private const string UserPreferenceCookieName = "gmsd_chatforwardui_pref";

    // Session key for storing user override preference
    private const string UserOverrideSessionKey = "ChatForwardUI_UserOverride";

    public FeatureFlagsController(
        IFeatureFlagService featureFlagService,
        ILogger<FeatureFlagsController> logger)
    {
        _featureFlagService = featureFlagService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current feature flag status for the ChatForwardUI feature.
    /// </summary>
    [HttpGet("chat-forward-ui")]
    public ActionResult<ChatForwardUIStatus> GetStatus()
    {
        var sessionId = HttpContext.Session.Id;
        var userOverride = GetUserOverridePreference();

        var isEnabled = _featureFlagService.IsChatForwardUIEnabled(sessionId, userOverride);
        var rolloutPercentage = _featureFlagService.GetRolloutPercentage();
        var allowUserOverride = _featureFlagService.IsUserOverrideAllowed();

        return Ok(new ChatForwardUIStatus
        {
            Enabled = isEnabled,
            UserOverride = userOverride,
            RolloutPercentage = rolloutPercentage,
            AllowUserOverride = allowUserOverride
        });
    }

    /// <summary>
    /// Sets the user's preference for the ChatForwardUI feature.
    /// </summary>
    [HttpPost("chat-forward-ui/preference")]
    public IActionResult SetPreference([FromBody] SetPreferenceRequest request)
    {
        if (!_featureFlagService.IsUserOverrideAllowed())
        {
            return BadRequest(new { error = "User override is not allowed for this feature." });
        }

        // Store in session for immediate effect
        HttpContext.Session.SetString(UserOverrideSessionKey, request.Enabled.ToString());

        // Store in cookie for persistence across sessions (30 days)
        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(30),
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = Request.IsHttps
        };
        Response.Cookies.Append(UserPreferenceCookieName, request.Enabled.ToString(), cookieOptions);

        _logger.LogInformation(
            "User set ChatForwardUI preference to {Enabled} for session {SessionId}",
            request.Enabled,
            HttpContext.Session.Id[..Math.Min(8, HttpContext.Session.Id.Length)]);

        return Ok(new { enabled = request.Enabled });
    }

    /// <summary>
    /// Clears the user's preference, reverting to rollout-based assignment.
    /// </summary>
    [HttpDelete("chat-forward-ui/preference")]
    public IActionResult ClearPreference()
    {
        // Remove from session
        HttpContext.Session.Remove(UserOverrideSessionKey);

        // Remove cookie
        var cookieOptions = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            HttpOnly = true,
            SameSite = SameSiteMode.Strict
        };
        Response.Cookies.Append(UserPreferenceCookieName, "", cookieOptions);

        _logger.LogInformation(
            "User cleared ChatForwardUI preference for session {SessionId}",
            HttpContext.Session.Id[..Math.Min(8, HttpContext.Session.Id.Length)]);

        return Ok(new { message = "Preference cleared. Using rollout-based assignment." });
    }

    /// <summary>
    /// Gets the user's override preference from session or cookie.
    /// </summary>
    private bool? GetUserOverridePreference()
    {
        // First check session (faster)
        if (HttpContext.Session.TryGetValue(UserOverrideSessionKey, out var sessionValue))
        {
            if (sessionValue.Length > 0 && bool.TryParse(System.Text.Encoding.UTF8.GetString(sessionValue), out var sessionPref))
            {
                return sessionPref;
            }
        }

        // Then check cookie (persists across sessions)
        if (Request.Cookies.TryGetValue(UserPreferenceCookieName, out var cookieValue))
        {
            if (bool.TryParse(cookieValue, out var cookiePref))
            {
                // Sync to session for faster access next time
                HttpContext.Session.SetString(UserOverrideSessionKey, cookieValue);
                return cookiePref;
            }
        }

        return null;
    }
}

/// <summary>
/// Response model for ChatForwardUI feature flag status.
/// </summary>
public class ChatForwardUIStatus
{
    /// <summary>
    /// Whether the ChatForwardUI is currently enabled for this user.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// The user's explicit preference override, if set.
    /// </summary>
    public bool? UserOverride { get; set; }

    /// <summary>
    /// The current rollout percentage (0-100).
    /// </summary>
    public int RolloutPercentage { get; set; }

    /// <summary>
    /// Whether users are allowed to override the feature flag.
    /// </summary>
    public bool AllowUserOverride { get; set; }
}

/// <summary>
/// Request model for setting user preference.
/// </summary>
public class SetPreferenceRequest
{
    /// <summary>
    /// Whether to enable the ChatForwardUI feature.
    /// </summary>
    public bool Enabled { get; set; }
}
