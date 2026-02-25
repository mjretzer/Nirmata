namespace Gmsd.Web.Models;

public class RunCardModel
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = "unknown";
    public string? Description { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public int FilesTouched { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? EvidenceUrl { get; set; }
    public string? DetailsUrl { get; set; }

    public string GetStatusClass()
    {
        return Status.ToLowerInvariant() switch
        {
            "success" or "completed" => "run-success",
            "running" or "in_progress" => "run-running",
            "failed" or "error" => "run-failed",
            "paused" => "run-paused",
            _ => "run-unknown"
        };
    }

    public string FormatDuration()
    {
        if (!Duration.HasValue) return "-";
        var d = Duration.Value;
        if (d.TotalHours >= 1) return $"{d.TotalHours:F1}h";
        if (d.TotalMinutes >= 1) return $"{d.TotalMinutes:F1}m";
        return $"{d.TotalSeconds:F0}s";
    }
}
