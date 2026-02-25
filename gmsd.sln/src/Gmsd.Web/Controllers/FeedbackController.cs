using Microsoft.AspNetCore.Mvc;
using Gmsd.Web.Models.Feedback;

namespace Gmsd.Web.Controllers;

/// <summary>
/// API controller for collecting user feedback and analytics
/// </summary>
[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private readonly ILogger<FeedbackController> _logger;
    private readonly IConfiguration _configuration;

    public FeedbackController(ILogger<FeedbackController> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Submit a user satisfaction survey
    /// </summary>
    [HttpPost("survey")]
    public IActionResult SubmitSurvey([FromBody] SatisfactionSurveyRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Log survey submission (in production, this would persist to database)
        _logger.LogInformation(
            "User satisfaction survey submitted - Rating: {Rating}, Category: {Category}, " +
            "Session: {SessionId}, Timestamp: {Timestamp}",
            request.Rating,
            request.Category ?? "general",
            HttpContext.Session.Id,
            DateTime.UtcNow);

        if (!string.IsNullOrEmpty(request.Comment))
        {
            _logger.LogDebug("Survey comment: {Comment}", request.Comment);
        }

        // Store last survey date in session
        HttpContext.Session.SetString("LastSurveyDate", DateTime.UtcNow.ToString("O"));

        return Ok(new { success = true, message = "Thank you for your feedback!" });
    }

    /// <summary>
    /// Submit general feedback (bug report, feature request, etc.)
    /// </summary>
    [HttpPost("general")]
    public IActionResult SubmitGeneralFeedback([FromBody] GeneralFeedbackRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        _logger.LogInformation(
            "General feedback submitted - Type: {Type}, Screen: {Screen}, " +
            "Session: {SessionId}, Timestamp: {Timestamp}",
            request.Type ?? "general",
            request.Screen ?? "unknown",
            HttpContext.Session.Id,
            DateTime.UtcNow);

        _logger.LogDebug("Feedback message: {Message}", request.Message);

        return Ok(new { success = true, message = "Feedback received. Thank you!" });
    }

    /// <summary>
    /// Submit task completion timing data (opt-in only)
    /// </summary>
    [HttpPost("timing")]
    public IActionResult SubmitTaskTiming([FromBody] TaskTimingRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if user has opted in to task timing
        var preferences = GetUserPreferences();
        if (!preferences.EnableTaskTiming)
        {
            return BadRequest(new { error = "Task timing is not enabled. User must opt-in first." });
        }

        var duration = request.EndTime - request.StartTime;

        _logger.LogInformation(
            "Task timing recorded - TaskId: {TaskId}, Type: {TaskType}, " +
            "Duration: {DurationMs}ms, Success: {Success}, Steps: {Steps}, " +
            "Session: {SessionId}",
            request.TaskId,
            request.TaskType,
            duration.TotalMilliseconds,
            request.WasSuccessful,
            request.StepCount,
            HttpContext.Session.Id);

        return Ok(new { success = true, durationMs = duration.TotalMilliseconds });
    }

    /// <summary>
    /// Submit analytics event (navigation clicks, etc.)
    /// </summary>
    [HttpPost("analytics")]
    public IActionResult SubmitAnalyticsEvent([FromBody] AnalyticsEventRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Check if user has opted in to analytics
        var preferences = GetUserPreferences();
        if (!preferences.EnableClickAnalytics)
        {
            return Ok(new { success = true, message = "Analytics not enabled, event ignored." });
        }

        _logger.LogDebug(
            "Analytics event - Type: {EventType}, Element: {ElementId}, " +
            "ElementType: {ElementType}, Screen: {Screen}, " +
            "Session: {SessionId}, Timestamp: {Timestamp}",
            request.EventType,
            request.ElementId,
            request.ElementType ?? "unknown",
            request.Screen ?? "unknown",
            HttpContext.Session.Id,
            request.Timestamp);

        return Ok(new { success = true });
    }

    /// <summary>
    /// Get user feedback preferences
    /// </summary>
    [HttpGet("preferences")]
    public IActionResult GetPreferences()
    {
        var preferences = GetUserPreferences();
        return Ok(preferences);
    }

    /// <summary>
    /// Update user feedback preferences (opt-in settings)
    /// </summary>
    [HttpPost("preferences")]
    public IActionResult UpdatePreferences([FromBody] FeedbackPreferences preferences)
    {
        // Store preferences in session
        HttpContext.Session.SetString("FeedbackPreferences", System.Text.Json.JsonSerializer.Serialize(preferences));

        _logger.LogInformation(
            "Feedback preferences updated - TaskTiming: {TaskTiming}, ClickAnalytics: {ClickAnalytics}, " +
            "Session: {SessionId}",
            preferences.EnableTaskTiming,
            preferences.EnableClickAnalytics,
            HttpContext.Session.Id);

        return Ok(new { success = true, preferences });
    }

    /// <summary>
    /// Check if user should be prompted for satisfaction survey
    /// </summary>
    [HttpGet("survey-eligible")]
    public IActionResult CheckSurveyEligibility()
    {
        var preferences = GetUserPreferences();
        var lastSurvey = preferences.LastSurveyDate;
        var dismissCount = preferences.SurveyDismissCount;

        // Don't show survey if:
        // 1. User has completed survey in last 30 days
        // 2. User has dismissed 3+ times without completing
        var isEligible = true;
        var reason = "";

        if (lastSurvey.HasValue && (DateTime.UtcNow - lastSurvey.Value).TotalDays < 30)
        {
            isEligible = false;
            reason = "Survey completed recently";
        }
        else if (dismissCount >= 3)
        {
            isEligible = false;
            reason = "Survey dismissed multiple times";
        }

        return Ok(new
        {
            isEligible,
            reason,
            lastSurveyDate = lastSurvey,
            dismissCount
        });
    }

    /// <summary>
    /// Record survey dismissal
    /// </summary>
    [HttpPost("survey-dismiss")]
    public IActionResult RecordSurveyDismissal()
    {
        var preferences = GetUserPreferences();
        preferences.SurveyDismissCount++;

        HttpContext.Session.SetString("FeedbackPreferences", System.Text.Json.JsonSerializer.Serialize(preferences));

        _logger.LogInformation("Survey dismissed - Count: {Count}, Session: {SessionId}",
            preferences.SurveyDismissCount,
            HttpContext.Session.Id);

        return Ok(new { success = true });
    }

    private FeedbackPreferences GetUserPreferences()
    {
        var preferencesJson = HttpContext.Session.GetString("FeedbackPreferences");
        if (!string.IsNullOrEmpty(preferencesJson))
        {
            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<FeedbackPreferences>(preferencesJson)
                    ?? new FeedbackPreferences();
            }
            catch
            {
                return new FeedbackPreferences();
            }
        }
        return new FeedbackPreferences();
    }
}
