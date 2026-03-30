using System.ComponentModel.DataAnnotations;
using nirmata.Data.Entities.Workspaces;

namespace nirmata.Data.Entities.Chat;

/// <summary>
/// A single persisted turn in a workspace chat thread.
/// Complex fields (Gate, Timeline, Artifacts, Logs) are stored as JSON text
/// so SQLite can hold them without a separate normalised schema.
/// </summary>
public class ChatMessage
{
    [Key]
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    /// <summary>Serialised <c>OrchestratorGateDto</c>; null for user-role messages.</summary>
    public string? GateJson { get; set; }

    /// <summary>Serialised string array of artifact references.</summary>
    public string ArtifactsJson { get; set; } = "[]";

    /// <summary>Serialised <c>OrchestratorTimelineDto</c>; null when no timeline was captured.</summary>
    public string? TimelineJson { get; set; }

    public string? NextCommand { get; set; }

    public string? RunId { get; set; }

    /// <summary>Serialised string array of log lines.</summary>
    public string LogsJson { get; set; } = "[]";

    public DateTimeOffset Timestamp { get; set; }

    public string? AgentId { get; set; }

    public virtual Workspace Workspace { get; set; } = null!;
}
