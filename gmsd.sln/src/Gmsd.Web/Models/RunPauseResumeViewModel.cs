namespace Gmsd.Web.Models;

/// <summary>
/// ViewModel for displaying and controlling pause/resume status of a run.
/// </summary>
public class RunPauseResumeViewModel
{
    /// <summary>
    /// The run ID.
    /// </summary>
    public string RunId { get; set; } = string.Empty;

    /// <summary>
    /// The current status of the run (started, paused, finished, abandoned).
    /// </summary>
    public string? CurrentStatus { get; set; }

    /// <summary>
    /// Whether the run can be paused (true if status is "started").
    /// </summary>
    public bool CanPause { get; set; }

    /// <summary>
    /// Whether the run can be resumed (true if status is "paused").
    /// </summary>
    public bool CanResume { get; set; }

    /// <summary>
    /// The timestamp of the last status change.
    /// </summary>
    public string? LastStatusChangeTime { get; set; }
}
