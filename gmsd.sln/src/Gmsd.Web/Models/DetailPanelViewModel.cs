namespace Gmsd.Web.Models;

/// <summary>
/// View model for the detail panel component
/// </summary>
public class DetailPanelViewModel
{
    /// <summary>
    /// Currently selected entity to display
    /// </summary>
    public EntityDetailModel? Entity { get; set; }

    /// <summary>
    /// Whether the panel has an entity loaded
    /// </summary>
    public bool HasEntity => Entity != null;

    /// <summary>
    /// Active tab name (properties, evidence, raw)
    /// </summary>
    public string ActiveTab { get; set; } = "properties";

    /// <summary>
    /// Whether the panel is collapsed to icon-only mode
    /// </summary>
    public bool IsCollapsed { get; set; } = true;
}

/// <summary>
/// Entity detail information for the panel
/// </summary>
public class EntityDetailModel
{
    /// <summary>
    /// Unique entity identifier
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Entity type (project, run, task, spec, etc.)
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Display name/title
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Entity status
    /// </summary>
    public string Status { get; set; } = "unknown";

    /// <summary>
    /// Properties to display in the Properties tab
    /// </summary>
    public Dictionary<string, PropertyValueModel> Properties { get; set; } = new();

    /// <summary>
    /// Evidence items for the Evidence tab
    /// </summary>
    public List<EvidenceItemModel> Evidence { get; set; } = new();

    /// <summary>
    /// Raw data for the Raw tab (typically JSON)
    /// </summary>
    public string RawData { get; set; } = "{}";

    /// <summary>
    /// Related entity references
    /// </summary>
    public List<RelatedEntityModel> RelatedEntities { get; set; } = new();

    /// <summary>
    /// Deep link URL to the entity's full page
    /// </summary>
    public string? FullPageUrl { get; set; }

    /// <summary>
    /// Timestamp when the entity was loaded
    /// </summary>
    public DateTime LoadedAt { get; set; } = DateTime.UtcNow;

    public string GetStatusClass() => Status.ToLowerInvariant() switch
    {
        "active" or "running" or "in_progress" => "status-active",
        "completed" or "success" => "status-completed",
        "failed" or "error" => "status-failed",
        "pending" or "queued" => "status-pending",
        "paused" => "status-paused",
        _ => "status-unknown"
    };

    public string GetTypeIcon() => Type.ToLowerInvariant() switch
    {
        "project" => "📁",
        "run" => "▶",
        "task" => "☐",
        "spec" => "📄",
        "issue" => "🐛",
        "milestone" => "🎯",
        "phase" => "🗓",
        "checkpoint" => "✓",
        "workspace" => "💼",
        _ => "📋"
    };
}

/// <summary>
/// Property value with optional link and formatting
/// </summary>
public class PropertyValueModel
{
    public string Value { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string? Tooltip { get; set; }
    public string Format { get; set; } = "text"; // text, code, date, duration, link
    public bool IsCopyable { get; set; } = false;
}

/// <summary>
/// Evidence item for the Evidence tab
/// </summary>
public class EvidenceItemModel
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // file, code, log, screenshot, artifact
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? FileUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Author { get; set; }

    public string GetTypeIcon() => Type.ToLowerInvariant() switch
    {
        "file" => "📄",
        "code" => "💻",
        "log" => "📝",
        "screenshot" => "📸",
        "artifact" => "📦",
        "test" => "🧪",
        _ => "📎"
    };
}

/// <summary>
/// Related entity reference
/// </summary>
public class RelatedEntityModel
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty; // parent, child, related
    public string? LinkUrl { get; set; }
}

/// <summary>
/// Entity summary for auto-population from chat context
/// </summary>
public class EntityMentionModel
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MentionText { get; set; } = string.Empty; // e.g., "run:abc-123"
    public int MentionCount { get; set; } = 1;
    public DateTime LastMentionedAt { get; set; } = DateTime.UtcNow;
}
