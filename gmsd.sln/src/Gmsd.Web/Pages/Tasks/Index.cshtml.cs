using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Tasks;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public List<TaskViewModel> Tasks { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterPhaseId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterMilestoneId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadTasks();
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

    private void LoadTasks()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var tasksPath = Path.Combine(WorkspacePath, ".aos", "spec", "tasks");
        var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");
        var roadmapPath = Path.Combine(WorkspacePath, ".aos", "spec", "roadmap.json");

        try
        {
            // Load phase-to-milestone mapping from roadmap
            var phaseMilestoneMap = LoadPhaseMilestoneMap(roadmapPath);

            // Load tasks from directory structure
            if (Directory.Exists(tasksPath))
            {
                var taskDirs = Directory.GetDirectories(tasksPath);
                foreach (var taskDir in taskDirs)
                {
                    var taskId = Path.GetFileName(taskDir);
                    var taskFile = Path.Combine(taskDir, "task.json");
                    
                    if (System.IO.File.Exists(taskFile))
                    {
                        var task = ParseTaskFile(taskId, taskFile, phaseMilestoneMap);
                        if (task != null)
                        {
                            Tasks.Add(task);
                        }
                    }
                }
            }

            // Load state for task statuses
            if (System.IO.File.Exists(statePath))
            {
                var stateJson = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);
                ApplyStateToTasks(stateDoc);
            }

            // Sort by task ID
            Tasks = Tasks.OrderBy(t => t.Id).ToList();

            // Apply filters
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tasks");
            ErrorMessage = $"Error loading tasks: {ex.Message}";
        }
    }

    private Dictionary<string, (string MilestoneId, string MilestoneName)> LoadPhaseMilestoneMap(string roadmapPath)
    {
        var map = new Dictionary<string, (string, string)>();

        if (!System.IO.File.Exists(roadmapPath))
        {
            return map;
        }

        try
        {
            var json = System.IO.File.ReadAllText(roadmapPath);
            var roadmapDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (roadmapDoc?.RootElement.TryGetProperty("roadmap", out var roadmap) == true &&
                roadmap.TryGetProperty("items", out var items))
            {
                string? currentMilestoneId = null;
                string? currentMilestoneName = null;

                foreach (var item in items.EnumerateArray())
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
                        if (currentMilestoneId != null)
                        {
                            map[id] = (currentMilestoneId, currentMilestoneName ?? "Unknown");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load phase-milestone mapping from roadmap");
        }

        return map;
    }

    private TaskViewModel? ParseTaskFile(string taskId, string taskFile, Dictionary<string, (string MilestoneId, string MilestoneName)> phaseMilestoneMap)
    {
        try
        {
            var json = System.IO.File.ReadAllText(taskFile);
            var taskDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (taskDoc == null) return null;

            var root = taskDoc.RootElement;
            
            // Try to get task property first (new format), then fall back to root (old format)
            JsonElement taskElement;
            if (root.TryGetProperty("task", out var taskProp))
            {
                taskElement = taskProp;
            }
            else
            {
                taskElement = root;
            }

            var task = new TaskViewModel
            {
                Id = taskId,
                Title = taskElement.TryGetProperty("title", out var title) 
                    ? title.GetString() ?? taskId 
                    : taskId,
                Description = taskElement.TryGetProperty("description", out var desc) 
                    ? desc.GetString() 
                    : null,
                Status = SpecItemStatus.Draft
            };

            // Get phase info
            if (taskElement.TryGetProperty("phaseId", out var phaseId))
            {
                task.PhaseId = phaseId.GetString() ?? "";
                
                // Look up milestone from phase
                if (phaseMilestoneMap.TryGetValue(task.PhaseId, out var milestoneInfo))
                {
                    task.MilestoneId = milestoneInfo.MilestoneId;
                    task.MilestoneName = milestoneInfo.MilestoneName;
                }

                // Try to get phase name from phase file
                var phaseFile = Path.Combine(WorkspacePath!, ".aos", "spec", "phases", $"{task.PhaseId}.json");
                if (System.IO.File.Exists(phaseFile))
                {
                    try
                    {
                        var phaseJson = System.IO.File.ReadAllText(phaseFile);
                        var phaseDoc = JsonSerializer.Deserialize<JsonDocument>(phaseJson);
                        if (phaseDoc?.RootElement.TryGetProperty("name", out var phaseName) == true)
                        {
                            task.PhaseName = phaseName.GetString() ?? task.PhaseId;
                        }
                        else
                        {
                            task.PhaseName = task.PhaseId;
                        }
                    }
                    catch
                    {
                        task.PhaseName = task.PhaseId;
                    }
                }
                else
                {
                    task.PhaseName = task.PhaseId;
                }
            }

            // Parse acceptance criteria
            if (taskElement.TryGetProperty("acceptanceCriteria", out var criteria))
            {
                task.AcceptanceCriteria = criteria.EnumerateArray()
                    .Select(c => c.GetString() ?? "")
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
            }

            // Parse status if present
            if (taskElement.TryGetProperty("status", out var status))
            {
                task.Status = ParseStatus(status.GetString());
            }

            // Parse dates
            if (taskElement.TryGetProperty("createdAt", out var createdAt))
            {
                if (createdAt.ValueKind == JsonValueKind.String && 
                    DateTime.TryParse(createdAt.GetString(), out var createdDate))
                {
                    task.CreatedAt = createdDate;
                }
            }

            if (taskElement.TryGetProperty("updatedAt", out var updatedAt))
            {
                if (updatedAt.ValueKind == JsonValueKind.String && 
                    DateTime.TryParse(updatedAt.GetString(), out var updatedDate))
                {
                    task.UpdatedAt = updatedDate;
                }
            }

            return task;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse task file {TaskFile}", taskFile);
            return null;
        }
    }

    private void ApplyStateToTasks(JsonDocument? stateDoc)
    {
        if (stateDoc == null) return;

        var root = stateDoc.RootElement;

        // Apply task statuses from state
        if (root.TryGetProperty("tasks", out var tasksElement))
        {
            foreach (var task in Tasks)
            {
                if (tasksElement.TryGetProperty(task.Id, out var taskState))
                {
                    if (taskState.TryGetProperty("status", out var status))
                    {
                        task.Status = ParseStatus(status.GetString());
                    }

                    // Load latest run info
                    if (taskState.TryGetProperty("latestRunId", out var runId))
                    {
                        task.LatestRunId = runId.GetString();
                    }

                    if (taskState.TryGetProperty("latestRunAt", out var runAt) && 
                        runAt.ValueKind == JsonValueKind.String &&
                        DateTime.TryParse(runAt.GetString(), out var runDate))
                    {
                        task.LatestRunAt = runDate;
                    }

                    if (taskState.TryGetProperty("latestRunStatus", out var runStatus))
                    {
                        task.LatestRunStatus = runStatus.GetString();
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
            "pending" => SpecItemStatus.Planned,
            "planned" => SpecItemStatus.Planned,
            "inprogress" or "in_progress" or "running" => SpecItemStatus.InProgress,
            "blocked" => SpecItemStatus.Blocked,
            "completed" or "success" => SpecItemStatus.Completed,
            "verified" => SpecItemStatus.Verified,
            "failed" or "error" => SpecItemStatus.Failed,
            _ => SpecItemStatus.Draft
        };
    }

    private void ApplyFilters()
    {
        if (!string.IsNullOrEmpty(FilterStatus))
        {
            Tasks = Tasks.Where(t => t.Status.ToString().Equals(FilterStatus, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(FilterPhaseId))
        {
            Tasks = Tasks.Where(t => t.PhaseId == FilterPhaseId).ToList();
        }

        if (!string.IsNullOrEmpty(FilterMilestoneId))
        {
            Tasks = Tasks.Where(t => t.MilestoneId == FilterMilestoneId).ToList();
        }

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            Tasks = Tasks.Where(t =>
                t.Id.ToLowerInvariant().Contains(query) ||
                t.Title.ToLowerInvariant().Contains(query) ||
                (t.Description?.ToLowerInvariant().Contains(query) ?? false) ||
                t.PhaseName.ToLowerInvariant().Contains(query) ||
                t.MilestoneName.ToLowerInvariant().Contains(query)
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
