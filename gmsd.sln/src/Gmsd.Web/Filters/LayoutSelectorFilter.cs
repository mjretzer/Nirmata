using Microsoft.AspNetCore.Mvc.Filters;
using Gmsd.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Filters;

/// <summary>
/// Page filter that dynamically selects the layout based on the ChatForwardUI feature flag.
/// This filter runs early in the page lifecycle to set the layout before rendering.
/// </summary>
public class LayoutSelectorFilter : IPageFilter
{
    private readonly IFeatureFlagService _featureFlagService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<LayoutSelectorFilter> _logger;

    // Session key for storing user override preference
    private const string UserOverrideSessionKey = "ChatForwardUI_UserOverride";

    // Cookie name for persisting user preference across sessions
    private const string UserPreferenceCookieName = "gmsd_chatforwardui_pref";

    public LayoutSelectorFilter(
        IFeatureFlagService featureFlagService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<LayoutSelectorFilter> logger)
    {
        _featureFlagService = featureFlagService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Called before the handler method executes. Sets the layout based on feature flag evaluation.
    /// </summary>
    public void OnPageHandlerSelected(PageHandlerSelectedContext context)
    {
        if (context.HandlerInstance is not PageModel pageModel)
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return;
        }

        // Get session ID for consistent rollout assignment
        var sessionId = httpContext.Session.Id;

        // Check for user override preference (from cookie or session)
        var userOverride = GetUserOverridePreference(httpContext);

        // Evaluate feature flag
        var useNewLayout = _featureFlagService.IsChatForwardUIEnabled(sessionId, userOverride);

        // Select layout based on feature flag
        var layoutName = useNewLayout ? "_MainLayout" : "_Layout";

        _logger.LogDebug(
            "Layout selected: {Layout} for session {SessionId} (override: {Override})",
            layoutName,
            sessionId[..Math.Min(8, sessionId.Length)],
            userOverride?.ToString() ?? "none");

        // Set the layout on the page model
        pageModel.ViewData["Layout"] = layoutName;
    }

    /// <summary>
    /// Called after the handler method executes. No action needed.
    /// </summary>
    public void OnPageHandlerExecuted(PageHandlerExecutedContext context)
    {
        // No post-handler actions needed
    }

    /// <summary>
    /// Called before the handler method executes. No action needed.
    /// </summary>
    public void OnPageHandlerExecuting(PageHandlerExecutingContext context)
    {
        // No pre-handler actions needed
    }

    /// <summary>
    /// Gets the user's override preference from session or cookie.
    /// </summary>
    private static bool? GetUserOverridePreference(HttpContext httpContext)
    {
        // First check session (faster)
        if (httpContext.Session.TryGetValue(UserOverrideSessionKey, out var sessionValue))
        {
            if (sessionValue.Length > 0 && bool.TryParse(System.Text.Encoding.UTF8.GetString(sessionValue), out var sessionPref))
            {
                return sessionPref;
            }
        }

        // Then check cookie (persists across sessions)
        if (httpContext.Request.Cookies.TryGetValue(UserPreferenceCookieName, out var cookieValue))
        {
            if (bool.TryParse(cookieValue, out var cookiePref))
            {
                // Sync to session for faster access next time
                httpContext.Session.SetString(UserOverrideSessionKey, cookieValue);
                return cookiePref;
            }
        }

        return null;
    }
}
