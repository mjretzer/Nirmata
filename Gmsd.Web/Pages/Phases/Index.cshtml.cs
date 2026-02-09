using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Phases;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public List<PhaseViewModel> Phases { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterMilestoneId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadPhases();
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

    private void LoadPhases()
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
                Phases = ParsePhasesFromRoadmap(roadmapDoc);
            }
            else
            {
                ErrorMessage = "Roadmap not found. Please initialize the workspace first.";
                return;
            }

            // Load state for phase statuses and constraints
            if (System.IO.File.Exists(statePath))
            {
                var stateJson = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);
                ApplyStateToPhases(stateDoc);
            }

            // Apply filters
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading phases");
            ErrorMessage = $"Error loading phases: {ex.Message}";
        }
    }

    private List<PhaseViewModel> ParsePhasesFromRoadmap(JsonDocument? doc)
    {
        var phases = new List<PhaseViewModel>();

        if (doc == null) return phases;

        var root = doc.RootElement;
        if (root.TryGetProperty("roadmap", out var roadmapElement) &&
            roadmapElement.TryGetProperty("items", out var itemsElement))
        {
            string? currentMilestoneId = null;
            string? currentMilestoneName = null;

            foreach (var item in itemsElement.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString() ?? string.Empty;
                var title = item.GetProperty("title").GetString() ?? string.Empty;
                var kind = item.GetProperty("kind").GetString() ?? string.Empty;

                if (kind.Equals("milestone", StringComparison.OrdinalIgnoreCase))
                {
                    currentMilestoneId = id;
                    currentMilestoneName = title;
                }
                else if (kind.Equals("phase", StringComparison.OrdinalIgnoreCase))
                {
                    var phase = new PhaseViewModel
                    {
                        Id = id,
                        Name = title,
                        Status = SpecItemStatus.Planned,
                        MilestoneId = currentMilestoneId ?? "",
                        MilestoneName = currentMilestoneName ?? ""
                    };

                    if (item.TryGetProperty("description", out var desc))
                    {
                        phase.Description = desc.GetString();
                    }

                    phases.Add(phase);
                }
            }
        }

        return phases;
    }

    private void ApplyStateToPhases(JsonDocument? stateDoc)
    {
        if (stateDoc == null) return;

        var root = stateDoc.RootElement;

        // Apply phase statuses from state
        if (root.TryGetProperty("phases", out var phasesElement))
        {
            foreach (var phase in Phases)
            {
                if (phasesElement.TryGetProperty(phase.Id, out var phaseState))
                {
                    if (phaseState.TryGetProperty("status", out var status))
                    {
                        phase.Status = ParseStatus(status.GetString());
                    }

                    // Load goals and outcomes if available
                    if (phaseState.TryGetProperty("goals", out var goals))
                    {
                        phase.Goals = goals.EnumerateArray().Select(g => g.GetString() ?? "").Where(g => !string.IsNullOrEmpty(g)).ToList();
                    }

                    if (phaseState.TryGetProperty("outcomes", out var outcomes))
                    {
                        phase.Outcomes = outcomes.EnumerateArray().Select(o => o.GetString() ?? "").Where(o => !string.IsNullOrEmpty(o)).ToList();
                    }

                    // Load assumptions
                    if (phaseState.TryGetProperty("assumptions", out var assumptions))
                    {
                        phase.Assumptions = assumptions.EnumerateArray().Select(a => a.GetString() ?? "").Where(a => !string.IsNullOrEmpty(a)).ToList();
                    }

                    // Load research items
                    if (phaseState.TryGetProperty("research", out var research))
                    {
                        foreach (var researchItem in research.EnumerateArray())
                        {
                            var topic = researchItem.GetProperty("topic").GetString() ?? "";
                            var findings = researchItem.TryGetProperty("findings", out var f) ? f.GetString() : null;
                            var isComplete = researchItem.TryGetProperty("isComplete", out var ic) && ic.GetBoolean();

                            if (!string.IsNullOrEmpty(topic))
                            {
                                phase.Research.Add(new ResearchItemViewModel
                                {
                                    Id = Guid.NewGuid().ToString()[..8],
                                    Topic = topic,
                                    Findings = findings,
                                    IsComplete = isComplete
                                });
                            }
                        }
                    }
                }
            }
        }

        // Load constraints from state
        if (root.TryGetProperty("constraints", out var constraintsElement))
        {
            foreach (var phase in Phases)
            {
                if (constraintsElement.TryGetProperty(phase.Id, out var phaseConstraints))
                {
                    foreach (var constraint in phaseConstraints.EnumerateArray())
                    {
                        var type = constraint.GetProperty("type").GetString() ?? "general";
                        var description = constraint.GetProperty("description").GetString() ?? "";
                        var isBlocking = constraint.TryGetProperty("isBlocking", out var ib) && ib.GetBoolean();
                        var source = constraint.TryGetProperty("source", out var s) ? s.GetString() : null;

                        if (!string.IsNullOrEmpty(description))
                        {
                            phase.Constraints.Add(new ConstraintViewModel
                            {
                                Type = type,
                                Description = description,
                                IsBlocking = isBlocking,
                                Source = source
                            });
                        }
                    }
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

    private void ApplyFilters()
    {
        if (!string.IsNullOrEmpty(FilterStatus))
        {
            Phases = Phases.Where(p => p.Status.ToString().Equals(FilterStatus, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(FilterMilestoneId))
        {
            Phases = Phases.Where(p => p.MilestoneId == FilterMilestoneId).ToList();
        }

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            Phases = Phases.Where(p =>
                p.Name.ToLowerInvariant().Contains(query) ||
                (p.Description?.ToLowerInvariant().Contains(query) ?? false) ||
                p.MilestoneName.ToLowerInvariant().Contains(query)
            ).ToList();
        }
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
