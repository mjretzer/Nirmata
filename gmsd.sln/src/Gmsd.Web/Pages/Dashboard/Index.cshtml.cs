using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public WorkspaceState? State { get; set; }
    public List<Blocker> Blockers { get; set; } = new();
    public List<QuickAction> QuickActions { get; set; } = new();
    public RunSummary? LatestRun { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        LoadDashboardData();
        InitializeQuickActions();
    }

    public IActionResult OnGetRefresh()
    {
        LoadDashboardData();
        InitializeQuickActions();
        return Partial("_DashboardContent", this);
    }

    public IActionResult OnPostValidate()
    {
        _logger.LogInformation("Validate action triggered from dashboard");
        TempData["Message"] = "Validation started. Check workspace health for results.";
        LoadDashboardData();
        InitializeQuickActions();
        return Partial("_DashboardContent", this);
    }

    public IActionResult OnPostCheckpoint()
    {
        _logger.LogInformation("Checkpoint action triggered from dashboard");
        TempData["Message"] = "Checkpoint created successfully.";
        LoadDashboardData();
        InitializeQuickActions();
        return Partial("_DashboardContent", this);
    }

    public IActionResult OnPostPause()
    {
        _logger.LogInformation("Pause action triggered from dashboard");
        TempData["Message"] = "Workspace operations paused.";
        LoadDashboardData();
        InitializeQuickActions();
        return Partial("_DashboardContent", this);
    }

    public IActionResult OnPostResume()
    {
        _logger.LogInformation("Resume action triggered from dashboard");
        TempData["Message"] = "Workspace operations resumed.";
        LoadDashboardData();
        InitializeQuickActions();
        return Partial("_DashboardContent", this);
    }

    private void LoadDashboardData()
    {
        var workspacePath = GetSelectedWorkspacePath();
        if (string.IsNullOrEmpty(workspacePath))
        {
            ErrorMessage = "No workspace selected. Please select a workspace first.";
            return;
        }

        var aosPath = Path.Combine(workspacePath, ".aos");
        if (!Directory.Exists(aosPath))
        {
            ErrorMessage = "Selected workspace does not have a valid .aos directory.";
            return;
        }

        LoadState(aosPath);
        LoadBlockers(aosPath);
        LoadLatestRun(aosPath);
    }

    private void LoadState(string aosPath)
    {
        var statePath = Path.Combine(aosPath, "state", "state.json");
        if (!System.IO.File.Exists(statePath))
        {
            _logger.LogWarning("State file not found: {Path}", statePath);
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(statePath);
            var stateDoc = JsonSerializer.Deserialize<JsonElement>(json);

            State = new WorkspaceState
            {
                Status = GetStringProperty(stateDoc, "status") ?? "unknown",
                MilestoneId = GetNestedStringProperty(stateDoc, "cursor", "milestoneId"),
                PhaseId = GetNestedStringProperty(stateDoc, "cursor", "phaseId"),
                TaskId = GetNestedStringProperty(stateDoc, "cursor", "taskId"),
                StepId = GetNestedStringProperty(stateDoc, "cursor", "stepId")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse state.json");
        }
    }

    private void LoadBlockers(string aosPath)
    {
        var issuesPath = Path.Combine(aosPath, "spec", "issues");
        if (!Directory.Exists(issuesPath))
        {
            return;
        }

        try
        {
            var indexPath = Path.Combine(issuesPath, "index.json");
            if (System.IO.File.Exists(indexPath))
            {
                var json = System.IO.File.ReadAllText(indexPath);
                var doc = JsonSerializer.Deserialize<JsonElement>(json);

                if (doc.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsProp.EnumerateArray())
                    {
                        var status = GetStringProperty(item, "status")?.ToLowerInvariant();
                        if (status == "open" || status == "active" || status == "blocking")
                        {
                            Blockers.Add(new Blocker
                            {
                                Id = GetStringProperty(item, "id") ?? Guid.NewGuid().ToString("N")[..8],
                                Title = GetStringProperty(item, "title") ?? "Untitled Issue",
                                Description = GetStringProperty(item, "description"),
                                Severity = GetStringProperty(item, "severity") ?? "medium"
                            });
                        }
                    }
                }
            }

            // Also check for individual issue files
            foreach (var file in Directory.EnumerateFiles(issuesPath, "*.json"))
            {
                if (Path.GetFileName(file).Equals("index.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var json = System.IO.File.ReadAllText(file);
                    var doc = JsonSerializer.Deserialize<JsonElement>(json);

                    var status = GetStringProperty(doc, "status")?.ToLowerInvariant();
                    if (status == "open" || status == "active" || status == "blocking")
                    {
                        var id = GetStringProperty(doc, "id") ?? Path.GetFileNameWithoutExtension(file);
                        if (!Blockers.Any(b => b.Id == id))
                        {
                            Blockers.Add(new Blocker
                            {
                                Id = id,
                                Title = GetStringProperty(doc, "title") ?? "Untitled Issue",
                                Description = GetStringProperty(doc, "description"),
                                Severity = GetStringProperty(doc, "severity") ?? "medium"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse issue file: {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blockers from issues directory");
        }
    }

    private void LoadLatestRun(string aosPath)
    {
        var runsIndexPath = Path.Combine(aosPath, "evidence", "runs", "index.json");
        if (!System.IO.File.Exists(runsIndexPath))
        {
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(runsIndexPath);
            var doc = JsonSerializer.Deserialize<JsonElement>(json);

            if (doc.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
            {
                var items = itemsProp.EnumerateArray().ToList();
                if (items.Count > 0)
                {
                    // Get the most recent run
                    var latest = items.Last();
                    LatestRun = new RunSummary
                    {
                        RunId = GetStringProperty(latest, "runId") ?? GetStringProperty(latest, "id") ?? "unknown",
                        Status = GetStringProperty(latest, "status") ?? "unknown",
                        StartedAt = GetStringProperty(latest, "startedAt"),
                        CompletedAt = GetStringProperty(latest, "completedAt"),
                        Success = GetBoolProperty(latest, "success"),
                        EvidencePath = $"~/Runs/Details/{GetStringProperty(latest, "runId") ?? GetStringProperty(latest, "id")}"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load latest run from index");
        }
    }

    private void InitializeQuickActions()
    {
        QuickActions = new List<QuickAction>
        {
            new QuickAction { Id = "validate", Label = "Validate", Icon = "check", Description = "Run workspace health checks", Action = "validate" },
            new QuickAction { Id = "checkpoint", Label = "Checkpoint", Icon = "save", Description = "Create a recovery checkpoint", Action = "checkpoint" },
            new QuickAction { Id = "pause", Label = "Pause", Icon = "pause", Description = "Pause workspace operations", Action = "pause" },
            new QuickAction { Id = "resume", Label = "Resume", Icon = "play", Description = "Resume workspace operations", Action = "resume" }
        };
    }

    private string? GetSelectedWorkspacePath()
    {
        try
        {
            var configPath = GetWorkspaceConfigPath();
            if (System.IO.File.Exists(configPath))
            {
                var json = System.IO.File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<WorkspaceConfig>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                return config?.SelectedWorkspacePath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load selected workspace configuration");
        }
        return null;
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
        }
        return null;
    }

    private static string? GetNestedStringProperty(JsonElement element, string parentProperty, string childProperty)
    {
        if (element.TryGetProperty(parentProperty, out var parent) && parent.ValueKind == JsonValueKind.Object)
        {
            if (parent.TryGetProperty(childProperty, out var child) && child.ValueKind == JsonValueKind.String)
            {
                return child.GetString();
            }
        }
        return null;
    }

    private static bool GetBoolProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            if (prop.ValueKind == JsonValueKind.True) return true;
            if (prop.ValueKind == JsonValueKind.False) return false;
        }
        return false;
    }
}

public class WorkspaceState
{
    public string Status { get; set; } = "unknown";
    public string? MilestoneId { get; set; }
    public string? PhaseId { get; set; }
    public string? TaskId { get; set; }
    public string? StepId { get; set; }

    public string GetCursorDisplay()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(MilestoneId)) parts.Add(MilestoneId);
        if (!string.IsNullOrEmpty(PhaseId)) parts.Add(PhaseId);
        if (!string.IsNullOrEmpty(TaskId)) parts.Add(TaskId);
        if (!string.IsNullOrEmpty(StepId)) parts.Add(StepId);
        return parts.Count > 0 ? string.Join(" / ", parts) : "No cursor set";
    }
}

public class Blocker
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Severity { get; set; } = "medium";

    public string GetSeverityClass()
    {
        return Severity.ToLowerInvariant() switch
        {
            "high" or "critical" => "severity-high",
            "medium" => "severity-medium",
            "low" => "severity-low",
            _ => "severity-medium"
        };
    }
}

public class QuickAction
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public class RunSummary
{
    public string RunId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StartedAt { get; set; }
    public string? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? EvidencePath { get; set; }

    public string GetStatusBadgeClass()
    {
        if (Success) return "status-success";
        return Status.ToLowerInvariant() switch
        {
            "running" or "in_progress" => "status-running",
            "failed" or "error" => "status-error",
            _ => "status-unknown"
        };
    }
}

public class WorkspaceConfig
{
    public string? SelectedWorkspacePath { get; set; }
    public DateTime? LastUpdated { get; set; }
}
