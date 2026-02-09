using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Milestones;

public class DetailsModel : PageModel
{
    private readonly ILogger<DetailsModel> _logger;
    private readonly IConfiguration _configuration;

    public DetailsModel(ILogger<DetailsModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public MilestoneViewModel Milestone { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public void OnGet(string id)
    {
        LoadWorkspace();
        LoadMilestone(id);
    }

    public IActionResult OnPostComplete(string id)
    {
        LoadWorkspace();

        try
        {
            var validationResult = ValidateCompletionGate(id);
            if (!validationResult.IsValid)
            {
                ErrorMessage = validationResult.ErrorMessage;
                LoadMilestone(id);
                return Page();
            }

            var result = CompleteMilestone(id);
            if (result)
            {
                SuccessMessage = "Milestone marked as complete.";
            }
            else
            {
                ErrorMessage = "Failed to complete milestone.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing milestone");
            ErrorMessage = $"Error completing milestone: {ex.Message}";
        }

        LoadMilestone(id);
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

    private void LoadMilestone(string milestoneId)
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
                Milestone = ParseMilestoneFromRoadmap(milestoneId, roadmapDoc);
            }
            else
            {
                ErrorMessage = "Roadmap not found.";
                return;
            }

            if (string.IsNullOrEmpty(Milestone.Id))
            {
                ErrorMessage = $"Milestone '{milestoneId}' not found.";
                return;
            }

            // Load state for phase statuses
            if (System.IO.File.Exists(statePath))
            {
                var stateJson = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);
                ApplyStateToMilestone(stateDoc);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading milestone {MilestoneId}", milestoneId);
            ErrorMessage = $"Error loading milestone: {ex.Message}";
        }
    }

    private MilestoneViewModel ParseMilestoneFromRoadmap(string milestoneId, JsonDocument? doc)
    {
        var milestone = new MilestoneViewModel();

        if (doc == null) return milestone;

        var root = doc.RootElement;
        if (root.TryGetProperty("roadmap", out var roadmapElement) &&
            roadmapElement.TryGetProperty("items", out var itemsElement))
        {
            bool foundMilestone = false;

            foreach (var item in itemsElement.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? string.Empty;
                var title = item.GetProperty("title").GetString() ?? string.Empty;
                var kind = item.GetProperty("kind").GetString() ?? string.Empty;

                if (kind.Equals("milestone", StringComparison.OrdinalIgnoreCase))
                {
                    // If we found a new milestone and already had our target, stop
                    if (foundMilestone)
                    {
                        break;
                    }

                    if (id == milestoneId)
                    {
                        foundMilestone = true;
                        milestone.Id = id;
                        milestone.Name = title;
                        milestone.Status = SpecItemStatus.Planned;

                        if (item.TryGetProperty("description", out var desc))
                        {
                            milestone.Description = desc.GetString();
                        }

                        if (item.TryGetProperty("targetDate", out var targetDate))
                        {
                            if (DateTime.TryParse(targetDate.GetString(), out var date))
                            {
                                milestone.TargetDate = date;
                            }
                        }
                    }
                }
                else if (kind.Equals("phase", StringComparison.OrdinalIgnoreCase) && foundMilestone)
                {
                    milestone.Phases.Add(new PhaseSummaryViewModel
                    {
                        Id = id,
                        Name = title,
                        Status = SpecItemStatus.Planned
                    });
                }
            }
        }

        return milestone;
    }

    private void ApplyStateToMilestone(JsonDocument? stateDoc)
    {
        if (stateDoc == null) return;

        var root = stateDoc.RootElement;

        // Update phase statuses
        if (root.TryGetProperty("phases", out var phasesElement))
        {
            foreach (var phase in Milestone.Phases)
            {
                if (phasesElement.TryGetProperty(phase.Id, out var phaseState))
                {
                    var statusValue = phaseState.GetProperty("status").GetString();
                    phase.Status = ParseStatus(statusValue);
                }
            }
        }

        // Calculate stats
        Milestone.TotalTasks = Milestone.Phases.Count;
        Milestone.CompletedTasks = Milestone.Phases.Count(p =>
            p.Status == SpecItemStatus.Completed || p.Status == SpecItemStatus.Verified);

        // Determine milestone status
        if (Milestone.CompletedTasks == Milestone.TotalTasks && Milestone.TotalTasks > 0)
        {
            Milestone.Status = SpecItemStatus.Completed;
        }
        else if (Milestone.CompletedTasks > 0)
        {
            Milestone.Status = SpecItemStatus.InProgress;
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

    private (bool IsValid, string? ErrorMessage) ValidateCompletionGate(string milestoneId)
    {
        LoadMilestone(milestoneId);

        if (Milestone.Phases.Count == 0)
        {
            return (true, null); // No phases means automatic completion
        }

        var incompletePhases = Milestone.Phases.Where(p =>
            p.Status != SpecItemStatus.Completed &&
            p.Status != SpecItemStatus.Verified).ToList();

        if (incompletePhases.Any())
        {
            var phaseNames = string.Join(", ", incompletePhases.Select(p => p.Name));
            return (false, $"Cannot complete milestone. Incomplete phases: {phaseNames}");
        }

        // Check for blocked phases
        var blockedPhases = Milestone.Phases.Where(p => p.Status == SpecItemStatus.Blocked).ToList();
        if (blockedPhases.Any())
        {
            var phaseNames = string.Join(", ", blockedPhases.Select(p => p.Name));
            return (false, $"Cannot complete milestone. Blocked phases: {phaseNames}");
        }

        Milestone.HasCompletionGate = true;
        Milestone.GateValidationMessage = "All phases completed successfully";
        return (true, null);
    }

    private bool CompleteMilestone(string milestoneId)
    {
        // In a full implementation, this would update the state.json file
        // For now, we just log the completion
        _logger.LogInformation("Milestone {MilestoneId} completed", milestoneId);
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
