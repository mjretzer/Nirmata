using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Milestones;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public List<MilestoneViewModel> Milestones { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public string? NewMilestoneName { get; set; }

    [BindProperty]
    public string? NewMilestoneDescription { get; set; }

    [BindProperty]
    public DateTime? NewMilestoneTargetDate { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadMilestones();
    }

    public IActionResult OnPostCreate()
    {
        LoadWorkspace();

        if (string.IsNullOrWhiteSpace(NewMilestoneName))
        {
            ErrorMessage = "Milestone name is required.";
            LoadMilestones();
            return Page();
        }

        try
        {
            var result = CreateMilestone(NewMilestoneName.Trim(), NewMilestoneDescription?.Trim(), NewMilestoneTargetDate);
            if (result)
            {
                SuccessMessage = $"Milestone '{NewMilestoneName.Trim()}' created successfully.";
            }
            else
            {
                ErrorMessage = "Failed to create milestone.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating milestone");
            ErrorMessage = $"Error creating milestone: {ex.Message}";
        }

        NewMilestoneName = null;
        NewMilestoneDescription = null;
        NewMilestoneTargetDate = null;
        LoadMilestones();
        return Page();
    }

    private void LoadWorkspace()
    {
        try
        {
            var configPath = GetWorkspaceConfigPath();
            if (System.IO.File.Exists(configPath))
            {
                var json = System.IO.File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<WorkspaceConfig>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                WorkspacePath = config?.SelectedWorkspacePath;
            }

            if (string.IsNullOrEmpty(WorkspacePath))
            {
                ErrorMessage = "No workspace selected. Please select a workspace first.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load workspace configuration");
            ErrorMessage = "Failed to load workspace configuration.";
        }
    }

    private void LoadMilestones()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        // Load from roadmap.json
        var roadmapPath = Path.Combine(WorkspacePath, ".aos", "spec", "roadmap.json");
        var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");

        try
        {
            if (System.IO.File.Exists(roadmapPath))
            {
                var json = System.IO.File.ReadAllText(roadmapPath);
                var roadmapDoc = JsonSerializer.Deserialize<JsonDocument>(json);
                Milestones = ParseMilestonesFromRoadmap(roadmapDoc);
            }
            else
            {
                ErrorMessage = "Roadmap not found. Please initialize the workspace first.";
                return;
            }

            // Load state for status info
            if (System.IO.File.Exists(statePath))
            {
                var stateJson = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);
                ApplyStateToMilestones(stateDoc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading milestones");
            ErrorMessage = $"Error loading milestones: {ex.Message}";
        }
    }

    private List<MilestoneViewModel> ParseMilestonesFromRoadmap(JsonDocument? doc)
    {
        var milestones = new List<MilestoneViewModel>();

        if (doc == null) return milestones;

        var root = doc.RootElement;
        if (root.TryGetProperty("roadmap", out var roadmapElement) &&
            roadmapElement.TryGetProperty("items", out var itemsElement))
        {
            MilestoneViewModel? currentMilestone = null;

            foreach (var item in itemsElement.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? string.Empty;
                var title = item.GetProperty("title").GetString() ?? string.Empty;
                var kind = item.GetProperty("kind").GetString() ?? string.Empty;

                if (kind.Equals("milestone", StringComparison.OrdinalIgnoreCase))
                {
                    currentMilestone = new MilestoneViewModel
                    {
                        Id = id,
                        Name = title,
                        Status = SpecItemStatus.Planned
                    };
                    milestones.Add(currentMilestone);
                }
                else if (kind.Equals("phase", StringComparison.OrdinalIgnoreCase) && currentMilestone != null)
                {
                    currentMilestone.Phases.Add(new PhaseSummaryViewModel
                    {
                        Id = id,
                        Name = title,
                        Status = SpecItemStatus.Planned
                    });
                }
            }
        }

        return milestones;
    }

    private void ApplyStateToMilestones(JsonDocument? stateDoc)
    {
        if (stateDoc == null) return;

        var root = stateDoc.RootElement;

        // Update phase statuses from state
        if (root.TryGetProperty("phases", out var phasesElement))
        {
            foreach (var milestone in Milestones)
            {
                foreach (var phase in milestone.Phases)
                {
                    if (phasesElement.TryGetProperty(phase.Id, out var phaseState))
                    {
                        var statusValue = phaseState.GetProperty("status").GetString();
                        phase.Status = ParseStatus(statusValue);
                    }
                }

                // Calculate milestone stats
                milestone.TotalTasks = milestone.Phases.Count;
                milestone.CompletedTasks = milestone.Phases.Count(p =>
                    p.Status == SpecItemStatus.Completed || p.Status == SpecItemStatus.Verified);

                // Determine milestone status
                if (milestone.CompletedTasks == milestone.TotalTasks && milestone.TotalTasks > 0)
                {
                    milestone.Status = SpecItemStatus.Completed;
                    milestone.HasCompletionGate = true;
                }
                else if (milestone.CompletedTasks > 0)
                {
                    milestone.Status = SpecItemStatus.InProgress;
                }
            }
        }
    }

    private SpecItemStatus ParseStatus(string? statusValue)
    {
        return statusValue?.ToLowerInvariant() switch
        {
            "draft" => SpecItemStatus.Draft,
            "planned" => SpecItemStatus.Planned,
            "inprogress" or "in_progress" => SpecItemStatus.InProgress,
            "blocked" => SpecItemStatus.Blocked,
            "completed" => SpecItemStatus.Completed,
            "verified" => SpecItemStatus.Verified,
            "failed" => SpecItemStatus.Failed,
            _ => SpecItemStatus.Planned
        };
    }

    private bool CreateMilestone(string name, string? description, DateTime? targetDate)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return false;

        var roadmapPath = Path.Combine(WorkspacePath, ".aos", "spec", "roadmap.json");
        if (!System.IO.File.Exists(roadmapPath)) return false;

        var json = System.IO.File.ReadAllText(roadmapPath);
        using var doc = JsonDocument.Parse(json);

        var newMilestoneId = $"MS-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

        var root = doc.RootElement;
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();

        if (root.TryGetProperty("schemaVersion", out var schemaVersion))
        {
            writer.WritePropertyName("schemaVersion");
            schemaVersion.WriteTo(writer);
        }

        writer.WritePropertyName("roadmap");
        if (root.TryGetProperty("roadmap", out var roadmap))
        {
            writer.WriteStartObject();

            if (roadmap.TryGetProperty("title", out var title))
            {
                writer.WritePropertyName("title");
                title.WriteTo(writer);
            }

            writer.WritePropertyName("items");
            writer.WriteStartArray();

            // Write existing items
            if (roadmap.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    item.WriteTo(writer);
                }
            }

            // Add new milestone
            writer.WriteStartObject();
            writer.WriteString("id", newMilestoneId);
            writer.WriteString("title", name);
            writer.WriteString("kind", "milestone");
            if (!string.IsNullOrEmpty(description))
            {
                writer.WriteString("description", description);
            }
            if (targetDate.HasValue)
            {
                writer.WriteString("targetDate", targetDate.Value.ToString("yyyy-MM-dd"));
            }
            writer.WriteEndObject();

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();

        var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        System.IO.File.WriteAllText(roadmapPath, newJson);

        return true;
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }

    private class WorkspaceConfig
    {
        public string? SelectedWorkspacePath { get; set; }
    }
}
