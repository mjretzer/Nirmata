namespace Gmsd.Web.Models;

/// <summary>
/// View model for the context sidebar component
/// </summary>
public class ContextSidebarViewModel
{
    /// <summary>
    /// Current workspace information
    /// </summary>
    public WorkspaceStatusModel? Workspace { get; set; }

    /// <summary>
    /// List of recent runs (last 5)
    /// </summary>
    public List<RecentRunModel> RecentRuns { get; set; } = new();

    /// <summary>
    /// Quick action commands
    /// </summary>
    public List<QuickActionModel> QuickActions { get; set; } = new();

    /// <summary>
    /// Section collapse states
    /// </summary>
    public Dictionary<string, bool> SectionStates { get; set; } = new();
}

/// <summary>
/// Workspace status information
/// </summary>
public class WorkspaceStatusModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown";
    public int ProjectCount { get; set; }
    public int OpenIssueCount { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public string? Cursor { get; set; }

    public string GetStatusClass() => Status.ToLowerInvariant() switch
    {
        "active" => "status-active",
        "inactive" => "status-inactive",
        "error" => "status-error",
        _ => "status-unknown"
    };

    public string GetRelativeTime()
    {
        if (!LastActivityAt.HasValue) return "Never";
        var diff = DateTime.UtcNow - LastActivityAt.Value;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{diff.TotalMinutes:0}m ago";
        if (diff.TotalDays < 1) return $"{diff.TotalHours:0}h ago";
        return $"{diff.TotalDays:0}d ago";
    }
}

/// <summary>
/// Recent run information for sidebar
/// </summary>
public class RecentRunModel
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown";
    public string? Description { get; set; }
    public DateTime StartedAt { get; set; }
    public TimeSpan? Duration { get; set; }

    public string GetStatusClass() => Status.ToLowerInvariant() switch
    {
        "success" or "completed" => "run-success",
        "running" or "in_progress" => "run-running",
        "failed" or "error" => "run-failed",
        "paused" => "run-paused",
        _ => "run-unknown"
    };

    public string GetStatusIcon() => Status.ToLowerInvariant() switch
    {
        "success" or "completed" => "✓",
        "running" or "in_progress" => "◌",
        "failed" or "error" => "✕",
        "paused" => "⏸",
        _ => "?"
    };

    public string FormatDuration()
    {
        if (!Duration.HasValue) return "-";
        var d = Duration.Value;
        if (d.TotalHours >= 1) return $"{d.TotalHours:F1}h";
        if (d.TotalMinutes >= 1) return $"{d.TotalMinutes:F1}m";
        return $"{d.TotalSeconds:F0}s";
    }

    public string GetRelativeTime()
    {
        var diff = DateTime.UtcNow - StartedAt;
        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalHours < 1) return $"{diff.TotalMinutes:0}m ago";
        if (diff.TotalDays < 1) return $"{diff.TotalHours:0}h ago";
        return $"{diff.TotalDays:0}d ago";
    }
}

/// <summary>
/// Quick action button model
/// </summary>
public class QuickActionModel
{
    public string Command { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "general";
}
