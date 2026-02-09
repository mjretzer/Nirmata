using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;
using Gmsd.Web.AgentRunner;

namespace Gmsd.Web.Pages.Tasks;

public class DetailsModel : PageModel
{
    private readonly ILogger<DetailsModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly WorkflowClassifier? _agentRunner;

    public DetailsModel(ILogger<DetailsModel> logger, IConfiguration configuration, WorkflowClassifier? agentRunner = null)
    {
        _logger = logger;
        _configuration = configuration;
        _agentRunner = agentRunner;
    }

    public TaskViewModel Task { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string ActiveTab { get; set; } = "task";

    public string? TaskJson { get; set; }
    public string? PlanJson { get; set; }
    public string? UatJson { get; set; }
    public string? LinksJson { get; set; }

    [BindProperty]
    public string? NewStatus { get; set; }

    public IActionResult OnGet(string id, string? tab = null)
    {
        ActiveTab = tab ?? "task";
        LoadWorkspace();
        LoadTask(id);

        if (string.IsNullOrEmpty(Task.Id))
        {
            return NotFound();
        }

        LoadTaskArtifacts(id);
        return Page();
    }

    public async Task<IActionResult> OnPostExecutePlanAsync(string id)
    {
        LoadWorkspace();
        LoadTask(id);

        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            LoadTaskArtifacts(id);
            return Page();
        }

        try
        {
            var result = await ExecuteTaskAsync(id);
            if (result.success)
            {
                SuccessMessage = $"Task executed successfully. Run ID: {result.runId}";
                Task.LatestRunId = result.runId;
                Task.LatestRunAt = DateTime.UtcNow;
                Task.LatestRunStatus = "completed";
                Task.Status = SpecItemStatus.Completed;
            }
            else
            {
                ErrorMessage = result.errorMessage ?? "Failed to execute task.";
                Task.Status = SpecItemStatus.Failed;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task {TaskId}", id);
            ErrorMessage = $"Error executing task: {ex.Message}";
        }

        LoadTaskArtifacts(id);
        return Page();
    }

    public IActionResult OnPostMarkStatus(string id)
    {
        LoadWorkspace();
        LoadTask(id);

        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            LoadTaskArtifacts(id);
            return Page();
        }

        if (string.IsNullOrEmpty(NewStatus))
        {
            ErrorMessage = "Status is required.";
            LoadTaskArtifacts(id);
            return Page();
        }

        try
        {
            var newStatusValue = ParseStatus(NewStatus);
            var result = PersistTaskStatus(id, newStatusValue);
            
            if (result)
            {
                SuccessMessage = $"Task status updated to {NewStatus}.";
                Task.Status = newStatusValue;
            }
            else
            {
                ErrorMessage = "Failed to update task status.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking task status for {TaskId}", id);
            ErrorMessage = $"Error updating status: {ex.Message}";
        }

        NewStatus = null;
        LoadTaskArtifacts(id);
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

    private void LoadTask(string taskId)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var tasksPath = Path.Combine(WorkspacePath, ".aos", "spec", "tasks");
        var taskFile = Path.Combine(tasksPath, taskId, "task.json");
        var roadmapPath = Path.Combine(WorkspacePath, ".aos", "spec", "roadmap.json");
        var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");

        if (!System.IO.File.Exists(taskFile))
        {
            ErrorMessage = $"Task {taskId} not found.";
            return;
        }

        try
        {
            // Load phase-to-milestone mapping
            var phaseMilestoneMap = LoadPhaseMilestoneMap(roadmapPath);

            var json = System.IO.File.ReadAllText(taskFile);
            var taskDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (taskDoc != null)
            {
                Task = ParseTaskDocument(taskId, taskDoc, phaseMilestoneMap);
            }

            // Load state for task status and run info
            if (System.IO.File.Exists(statePath))
            {
                var stateJson = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);
                ApplyStateToTask(stateDoc, taskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading task {TaskId}", taskId);
            ErrorMessage = $"Error loading task: {ex.Message}";
        }
    }

    private void LoadTaskArtifacts(string taskId)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var taskDir = Path.Combine(WorkspacePath, ".aos", "spec", "tasks", taskId);

        // Load task.json (raw)
        var taskFile = Path.Combine(taskDir, "task.json");
        if (System.IO.File.Exists(taskFile))
        {
            try
            {
                TaskJson = System.IO.File.ReadAllText(taskFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load task.json for {TaskId}", taskId);
            }
        }

        // Load plan.json
        var planFile = Path.Combine(taskDir, "plan.json");
        if (System.IO.File.Exists(planFile))
        {
            try
            {
                PlanJson = System.IO.File.ReadAllText(planFile);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plan.json for {TaskId}", taskId);
            }
        }

        // Load uat.json
        var uatFile = Path.Combine(taskDir, "uat.json");
        if (System.IO.File.Exists(uatFile))
        {
            try
            {
                UatJson = System.IO.File.ReadAllText(uatFile);
                
                // Parse verification steps from uat.json
                var uatDoc = JsonSerializer.Deserialize<JsonDocument>(UatJson);
                if (uatDoc?.RootElement.TryGetProperty("uat", out var uat) == true &&
                    uat.TryGetProperty("checks", out var checks))
                {
                    Task.VerificationSteps = checks.EnumerateArray()
                        .Select(c => c.TryGetProperty("description", out var desc) ? desc.GetString() : null)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .ToList()!;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load uat.json for {TaskId}", taskId);
            }
        }

        // Load links.json
        var linksFile = Path.Combine(taskDir, "links.json");
        if (System.IO.File.Exists(linksFile))
        {
            try
            {
                LinksJson = System.IO.File.ReadAllText(linksFile);
                
                // Parse links
                var linksDoc = JsonSerializer.Deserialize<JsonDocument>(LinksJson);
                if (linksDoc?.RootElement.TryGetProperty("links", out var links) == true)
                {
                    Task.Links = links.EnumerateArray()
                        .Select(link => new LinkViewModel
                        {
                            Type = link.TryGetProperty("type", out var type) ? type.GetString() ?? "" : "",
                            TargetId = link.TryGetProperty("targetId", out var targetId) ? targetId.GetString() ?? "" : "",
                            TargetName = link.TryGetProperty("targetName", out var targetName) 
                                ? targetName.GetString() 
                                : (link.TryGetProperty("targetId", out var tid) ? tid.GetString() : null),
                            Relationship = link.TryGetProperty("relationship", out var rel) ? rel.GetString() ?? "" : ""
                        })
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load links.json for {TaskId}", taskId);
            }
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

    private TaskViewModel ParseTaskDocument(string taskId, JsonDocument doc, Dictionary<string, (string MilestoneId, string MilestoneName)> phaseMilestoneMap)
    {
        var root = doc.RootElement;
        
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

            // Try to get phase name
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

        return task;
    }

    private void ApplyStateToTask(JsonDocument? stateDoc, string taskId)
    {
        if (stateDoc == null) return;

        var root = stateDoc.RootElement;

        // Apply task status from state
        if (root.TryGetProperty("tasks", out var tasksElement) &&
            tasksElement.TryGetProperty(taskId, out var taskState))
        {
            if (taskState.TryGetProperty("status", out var status))
            {
                Task.Status = ParseStatus(status.GetString());
            }

            // Load latest run info
            if (taskState.TryGetProperty("latestRunId", out var runId))
            {
                Task.LatestRunId = runId.GetString();
            }

            if (taskState.TryGetProperty("latestRunAt", out var runAt) && 
                runAt.ValueKind == JsonValueKind.String &&
                DateTime.TryParse(runAt.GetString(), out var runDate))
            {
                Task.LatestRunAt = runDate;
            }

            if (taskState.TryGetProperty("latestRunStatus", out var runStatus))
            {
                Task.LatestRunStatus = runStatus.GetString();
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

    private async Task<(bool success, string? runId, string? errorMessage)> ExecuteTaskAsync(string taskId)
    {
        try
        {
            if (_agentRunner == null)
            {
                return (false, null, "Agent runner is not available.");
            }

                        // Use the WorkflowClassifier to execute the task through the orchestrator
            var command = $"run execute --task-id {taskId}";
            var result = await _agentRunner.ExecuteAsync(command);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Executed task {TaskId}, run {RunId} completed successfully", taskId, result.RunId);
                return (true, result.RunId, null);
            }
            else
            {
                _logger.LogWarning("Task {TaskId} execution failed: {FinalPhase}", taskId, result.FinalPhase);
                return (false, result.RunId, $"Execution failed at phase: {result.FinalPhase}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing task {TaskId}", taskId);
            return (false, null, ex.Message);
        }
    }

    private bool PersistTaskStatus(string taskId, SpecItemStatus newStatus)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return false;

        try
        {
            var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");
            JsonDocument? stateDoc = null;

            if (System.IO.File.Exists(statePath))
            {
                var json = System.IO.File.ReadAllText(statePath);
                stateDoc = JsonSerializer.Deserialize<JsonDocument>(json);
            }

            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            // Copy existing state
            if (stateDoc != null)
            {
                var root = stateDoc.RootElement;

                // Copy all existing properties except tasks (we'll merge)
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "tasks")
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }
            }

            // Write tasks with updated status
            writer.WritePropertyName("tasks");
            writer.WriteStartObject();

            // Copy existing tasks
            if (stateDoc?.RootElement.TryGetProperty("tasks", out var tasks) == true)
            {
                foreach (var task in tasks.EnumerateObject())
                {
                    writer.WritePropertyName(task.Name);

                    if (task.Name == taskId)
                    {
                        // Merge existing task data with new status
                        writer.WriteStartObject();

                        // Copy existing properties
                        foreach (var prop in task.Value.EnumerateObject())
                        {
                            if (prop.Name != "status")
                            {
                                writer.WritePropertyName(prop.Name);
                                prop.Value.WriteTo(writer);
                            }
                        }

                        // Write new status
                        writer.WriteString("status", newStatus.ToString().ToLowerInvariant());
                        
                        // Write timestamp
                        writer.WriteString("updatedAt", DateTime.UtcNow.ToString("O"));

                        writer.WriteEndObject();
                    }
                    else
                    {
                        task.Value.WriteTo(writer);
                    }
                }
            }

            // If task doesn't exist, create it
            if (stateDoc?.RootElement.TryGetProperty("tasks", out var tasksCheck) != true ||
                !tasksCheck.EnumerateObject().Any(t => t.Name == taskId))
            {
                writer.WritePropertyName(taskId);
                writer.WriteStartObject();
                writer.WriteString("status", newStatus.ToString().ToLowerInvariant());
                writer.WriteString("updatedAt", DateTime.UtcNow.ToString("O"));
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();

            var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            System.IO.File.WriteAllText(statePath, newJson);

            // Append to events
            AppendStatusEvent(taskId, newStatus.ToString());

            _logger.LogInformation("Updated task {TaskId} status to {Status}", taskId, newStatus);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting status for task {TaskId}", taskId);
            return false;
        }
    }

    private void PersistTaskRun(string taskId, string runId, string runStatus)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return;

        try
        {
            var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");
            JsonDocument? stateDoc = null;

            if (System.IO.File.Exists(statePath))
            {
                var json = System.IO.File.ReadAllText(statePath);
                stateDoc = JsonSerializer.Deserialize<JsonDocument>(json);
            }

            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            // Copy existing state
            if (stateDoc != null)
            {
                var root = stateDoc.RootElement;
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Name != "tasks")
                    {
                        writer.WritePropertyName(property.Name);
                        property.Value.WriteTo(writer);
                    }
                }
            }

            // Write tasks with run info
            writer.WritePropertyName("tasks");
            writer.WriteStartObject();

            if (stateDoc?.RootElement.TryGetProperty("tasks", out var tasks) == true)
            {
                foreach (var task in tasks.EnumerateObject())
                {
                    writer.WritePropertyName(task.Name);

                    if (task.Name == taskId)
                    {
                        writer.WriteStartObject();

                        foreach (var prop in task.Value.EnumerateObject())
                        {
                            writer.WritePropertyName(prop.Name);
                            prop.Value.WriteTo(writer);
                        }

                        // Update run info
                        writer.WriteString("latestRunId", runId);
                        writer.WriteString("latestRunAt", DateTime.UtcNow.ToString("O"));
                        writer.WriteString("latestRunStatus", runStatus);

                        writer.WriteEndObject();
                    }
                    else
                    {
                        task.Value.WriteTo(writer);
                    }
                }
            }

            // Add task if not exists
            if (stateDoc?.RootElement.TryGetProperty("tasks", out var tasksCheck) != true ||
                !tasksCheck.EnumerateObject().Any(t => t.Name == taskId))
            {
                writer.WritePropertyName(taskId);
                writer.WriteStartObject();
                writer.WriteString("status", "completed");
                writer.WriteString("latestRunId", runId);
                writer.WriteString("latestRunAt", DateTime.UtcNow.ToString("O"));
                writer.WriteString("latestRunStatus", runStatus);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();

            var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            System.IO.File.WriteAllText(statePath, newJson);

            _logger.LogInformation("Updated task {TaskId} run info: {RunId}", taskId, runId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting run info for task {TaskId}", taskId);
        }
    }

    private void AppendStatusEvent(string taskId, string newStatus)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return;

        try
        {
            var eventsPath = Path.Combine(WorkspacePath, ".aos", "state", "events.ndjson");
            var eventLine = $"{{\"timestamp\":\"{DateTime.UtcNow:O}\",\"type\":\"task_status_changed\",\"taskId\":\"{taskId}\",\"newStatus\":\"{newStatus}\"}}" + Environment.NewLine;
            System.IO.File.AppendAllText(eventsPath, eventLine);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to append status event for task {TaskId}", taskId);
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
