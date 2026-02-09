using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Roadmap;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public RoadmapViewModel Roadmap { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty]
    public string? NewPhaseName { get; set; }

    [BindProperty]
    public string? InsertAfterPhaseId { get; set; }

    [BindProperty]
    public string? RemovePhaseId { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadRoadmap();
    }

    public IActionResult OnPostAddPhase()
    {
        LoadWorkspace();

        if (string.IsNullOrWhiteSpace(NewPhaseName))
        {
            ErrorMessage = "Phase name is required.";
            LoadRoadmap();
            return Page();
        }

        if (NewPhaseName.Length > 100)
        {
            ErrorMessage = "Phase name cannot exceed 100 characters.";
            LoadRoadmap();
            return Page();
        }

        try
        {
            var result = AddPhaseToRoadmap(NewPhaseName.Trim());
            if (result)
            {
                SuccessMessage = $"Phase '{NewPhaseName.Trim()}' added successfully.";
            }
            else
            {
                ErrorMessage = "Failed to add phase.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding phase");
            ErrorMessage = $"Error adding phase: {ex.Message}";
        }

        NewPhaseName = null;
        LoadRoadmap();
        return Page();
    }

    public IActionResult OnPostInsertPhase()
    {
        LoadWorkspace();

        if (string.IsNullOrWhiteSpace(NewPhaseName))
        {
            ErrorMessage = "Phase name is required.";
            LoadRoadmap();
            return Page();
        }

        if (string.IsNullOrWhiteSpace(InsertAfterPhaseId))
        {
            ErrorMessage = "Please select a phase to insert after.";
            LoadRoadmap();
            return Page();
        }

        if (NewPhaseName.Length > 100)
        {
            ErrorMessage = "Phase name cannot exceed 100 characters.";
            LoadRoadmap();
            return Page();
        }

        try
        {
            var result = InsertPhaseAfter(InsertAfterPhaseId, NewPhaseName.Trim());
            if (result)
            {
                SuccessMessage = $"Phase '{NewPhaseName.Trim()}' inserted successfully.";
            }
            else
            {
                ErrorMessage = "Failed to insert phase.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inserting phase");
            ErrorMessage = $"Error inserting phase: {ex.Message}";
        }

        NewPhaseName = null;
        InsertAfterPhaseId = null;
        LoadRoadmap();
        return Page();
    }

    public IActionResult OnPostRemovePhase()
    {
        LoadWorkspace();

        if (string.IsNullOrWhiteSpace(RemovePhaseId))
        {
            ErrorMessage = "Please select a phase to remove.";
            LoadRoadmap();
            return Page();
        }

        try
        {
            var result = RemovePhase(RemovePhaseId);
            if (result)
            {
                SuccessMessage = "Phase removed successfully.";
            }
            else
            {
                ErrorMessage = "Failed to remove phase. Phase may not exist or cannot be removed.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing phase");
            ErrorMessage = $"Error removing phase: {ex.Message}";
        }

        RemovePhaseId = null;
        LoadRoadmap();
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

    private void LoadRoadmap()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var roadmapPath = Path.Combine(WorkspacePath, ".aos", "spec", "roadmap.json");
        var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");

        try
        {
            if (System.IO.File.Exists(roadmapPath))
            {
                var json = System.IO.File.ReadAllText(roadmapPath);
                var roadmapDoc = JsonSerializer.Deserialize<JsonDocument>(json);
                Roadmap = ParseRoadmapDocument(roadmapDoc);
            }
            else
            {
                ErrorMessage = "Roadmap not found. Please initialize the workspace first.";
                return;
            }

            // Load state for cursor and alignment warnings
            if (System.IO.File.Exists(statePath))
            {
                var stateJson = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);
                ApplyStateToRoadmap(Roadmap, stateDoc);
            }

            GenerateAlignmentWarnings();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading roadmap");
            ErrorMessage = $"Error loading roadmap: {ex.Message}";
        }
    }

    private RoadmapViewModel ParseRoadmapDocument(JsonDocument? doc)
    {
        var roadmap = new RoadmapViewModel();

        if (doc == null) return roadmap;

        var root = doc.RootElement;
        if (root.TryGetProperty("roadmap", out var roadmapElement))
        {
            if (roadmapElement.TryGetProperty("items", out var itemsElement))
            {
                foreach (var item in itemsElement.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString() ?? string.Empty;
                    var title = item.GetProperty("title").GetString() ?? string.Empty;
                    var kind = item.GetProperty("kind").GetString() ?? string.Empty;

                    if (kind.Equals("milestone", StringComparison.OrdinalIgnoreCase))
                    {
                        var milestone = new MilestoneTimelineViewModel
                        {
                            Id = id,
                            Name = title,
                            Status = SpecItemStatus.Planned
                        };
                        roadmap.Milestones.Add(milestone);
                    }
                    else if (kind.Equals("phase", StringComparison.OrdinalIgnoreCase))
                    {
                        // Add phase to the last milestone, or create a default one
                        var phase = new PhaseTimelineViewModel
                        {
                            Id = id,
                            Name = title,
                            Status = SpecItemStatus.Planned
                        };

                        if (roadmap.Milestones.Count == 0)
                        {
                            roadmap.Milestones.Add(new MilestoneTimelineViewModel
                            {
                                Id = "default",
                                Name = "Default Milestone",
                                Status = SpecItemStatus.Planned,
                                Phases = { phase }
                            });
                        }
                        else
                        {
                            roadmap.Milestones.Last().Phases.Add(phase);
                        }
                    }
                }
            }
        }

        return roadmap;
    }

    private void ApplyStateToRoadmap(RoadmapViewModel roadmap, JsonDocument? stateDoc)
    {
        if (stateDoc == null) return;

        var root = stateDoc.RootElement;

        // Extract cursor information
        if (root.TryGetProperty("cursor", out var cursorElement))
        {
            if (cursorElement.TryGetProperty("phaseId", out var phaseIdElement))
            {
                roadmap.CurrentPhaseId = phaseIdElement.GetString();
            }

            if (cursorElement.TryGetProperty("milestoneId", out var milestoneIdElement))
            {
                roadmap.CurrentMilestoneId = milestoneIdElement.GetString();
            }
        }

        // Update phase statuses from state
        if (root.TryGetProperty("phases", out var phasesElement))
        {
            foreach (var phaseState in phasesElement.EnumerateObject())
            {
                var phaseId = phaseState.Name;
                var statusValue = phaseState.Value.GetProperty("status").GetString();
                var status = ParseStatus(statusValue);

                foreach (var milestone in roadmap.Milestones)
                {
                    var phase = milestone.Phases.FirstOrDefault(p => p.Id == phaseId);
                    if (phase != null)
                    {
                        phase.Status = status;
                    }
                }
            }
        }

        // Mark current phase as active
        if (!string.IsNullOrEmpty(roadmap.CurrentPhaseId))
        {
            foreach (var milestone in roadmap.Milestones)
            {
                var currentPhase = milestone.Phases.FirstOrDefault(p => p.Id == roadmap.CurrentPhaseId);
                if (currentPhase != null)
                {
                    currentPhase.Status = SpecItemStatus.InProgress;
                }
            }
        }

        // Calculate progress
        foreach (var milestone in roadmap.Milestones)
        {
            if (milestone.Phases.Count > 0)
            {
                var completedCount = milestone.Phases.Count(p =>
                    p.Status == SpecItemStatus.Completed ||
                    p.Status == SpecItemStatus.Verified);
                milestone.ProgressPercent = (completedCount * 100) / milestone.Phases.Count;

                // Milestone status based on phases
                if (milestone.ProgressPercent == 100)
                {
                    milestone.Status = SpecItemStatus.Completed;
                }
                else if (milestone.ProgressPercent > 0)
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

    private void GenerateAlignmentWarnings()
    {
        if (string.IsNullOrEmpty(Roadmap.CurrentPhaseId) && Roadmap.Milestones.Count > 0)
        {
            Roadmap.Warnings.Add(new AlignmentWarningViewModel
            {
                Type = "Cursor",
                Message = "No current phase set in state. The roadmap cursor needs to be initialized.",
                SuggestedAction = "Run 'aos state init-cursor' to set the initial phase."
            });
        }

        // Check for phases in state but not in roadmap
        var roadmapPhaseIds = Roadmap.Milestones
            .SelectMany(m => m.Phases)
            .Select(p => p.Id)
            .ToHashSet();

        // This would need state phase IDs - simplified check
        if (!string.IsNullOrEmpty(Roadmap.CurrentPhaseId) && !roadmapPhaseIds.Contains(Roadmap.CurrentPhaseId))
        {
            Roadmap.Warnings.Add(new AlignmentWarningViewModel
            {
                Type = "Alignment",
                Message = $"Current phase '{Roadmap.CurrentPhaseId}' in state does not exist in roadmap.",
                EntityId = Roadmap.CurrentPhaseId,
                EntityType = "Phase",
                SuggestedAction = "Update the cursor to a valid phase or refresh the roadmap."
            });
        }
    }

    private bool AddPhaseToRoadmap(string phaseName)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return false;

        var roadmapPath = Path.Combine(WorkspacePath, ".aos", "spec", "roadmap.json");
        if (!System.IO.File.Exists(roadmapPath)) return false;

        var json = System.IO.File.ReadAllText(roadmapPath);
        using var doc = JsonDocument.Parse(json);

        // Create new phase with ID
        var newPhaseId = $"PH-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

        // Parse and modify
        var options = new JsonSerializerOptions { WriteIndented = true };
        var root = doc.RootElement;

        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        writer.WriteStartObject();

        // Copy schemaVersion
        if (root.TryGetProperty("schemaVersion", out var schemaVersion))
        {
            writer.WritePropertyName("schemaVersion");
            schemaVersion.WriteTo(writer);
        }

        // Copy and modify roadmap
        writer.WritePropertyName("roadmap");
        if (root.TryGetProperty("roadmap", out var roadmap))
        {
            writer.WriteStartObject();

            // Copy title
            if (roadmap.TryGetProperty("title", out var title))
            {
                writer.WritePropertyName("title");
                title.WriteTo(writer);
            }

            // Modify items
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

            // Add new phase
            writer.WriteStartObject();
            writer.WriteString("id", newPhaseId);
            writer.WriteString("title", phaseName);
            writer.WriteString("kind", "phase");
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

    private bool InsertPhaseAfter(string afterPhaseId, string phaseName)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return false;

        var roadmapPath = Path.Combine(WorkspacePath, ".aos", "spec", "roadmap.json");
        if (!System.IO.File.Exists(roadmapPath)) return false;

        var json = System.IO.File.ReadAllText(roadmapPath);
        using var doc = JsonDocument.Parse(json);

        var newPhaseId = $"PH-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

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

            if (roadmap.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    item.WriteTo(writer);

                    // Insert after the specified phase
                    var id = item.GetProperty("id").GetString();
                    if (id == afterPhaseId)
                    {
                        writer.WriteStartObject();
                        writer.WriteString("id", newPhaseId);
                        writer.WriteString("title", phaseName);
                        writer.WriteString("kind", "phase");
                        writer.WriteEndObject();
                    }
                }
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.Flush();

        var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        System.IO.File.WriteAllText(roadmapPath, newJson);

        return true;
    }

    private bool RemovePhase(string phaseId)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return false;

        // Prevent removing the current phase
        if (phaseId == Roadmap.CurrentPhaseId)
        {
            throw new InvalidOperationException("Cannot remove the currently active phase.");
        }

        // Check if phase has completed work
        foreach (var milestone in Roadmap.Milestones)
        {
            var phase = milestone.Phases.FirstOrDefault(p => p.Id == phaseId);
            if (phase != null)
            {
                if (phase.Status == SpecItemStatus.Completed || phase.Status == SpecItemStatus.Verified)
                {
                    throw new InvalidOperationException("Cannot remove a completed or verified phase.");
                }
            }
        }

        var roadmapPath = Path.Combine(WorkspacePath, ".aos", "spec", "roadmap.json");
        if (!System.IO.File.Exists(roadmapPath)) return false;

        var json = System.IO.File.ReadAllText(roadmapPath);
        using var doc = JsonDocument.Parse(json);

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

            if (roadmap.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var id = item.GetProperty("id").GetString();
                    if (id != phaseId)
                    {
                        item.WriteTo(writer);
                    }
                }
            }

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
