using System.ComponentModel.DataAnnotations;

namespace Gmsd.Web.Models.Feedback;

/// <summary>
/// User satisfaction survey submission
/// </summary>
public class SatisfactionSurveyRequest
{
    [Required]
    [Range(1, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool AllowFollowUp { get; set; }

    [MaxLength(255)]
    public string? ContactEmail { get; set; }
}

/// <summary>
/// General feedback submission
/// </summary>
public class GeneralFeedbackRequest
{
    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Type { get; set; }

    [MaxLength(50)]
    public string? Screen { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }
}

/// <summary>
/// Task completion timing data (opt-in)
/// </summary>
public class TaskTimingRequest
{
    [Required]
    [MaxLength(100)]
    public string TaskId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string TaskType { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool WasSuccessful { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public int? StepCount { get; set; }
}

/// <summary>
/// Navigation/click analytics event
/// </summary>
public class AnalyticsEventRequest
{
    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ElementId { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ElementType { get; set; }

    [MaxLength(200)]
    public string? PageUrl { get; set; }

    [MaxLength(100)]
    public string? Screen { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User feedback preferences (opt-in settings)
/// </summary>
public class FeedbackPreferences
{
    public bool EnableTaskTiming { get; set; }

    public bool EnableClickAnalytics { get; set; }

    public bool EnableDetailedLogging { get; set; }

    public DateTime? LastSurveyDate { get; set; }

    public int SurveyDismissCount { get; set; }
}
