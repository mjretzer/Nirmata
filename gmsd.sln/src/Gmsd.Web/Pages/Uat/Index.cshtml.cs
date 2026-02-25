using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;

namespace Gmsd.Web.Pages.Uat;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public List<UatSessionViewModel> UatSessions { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterStatus { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? FilterTaskId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchQuery { get; set; }

    public void OnGet()
    {
        LoadWorkspace();
        LoadUatSessions();
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

    private void LoadUatSessions()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var uatPath = Path.Combine(WorkspacePath, ".aos", "spec", "uat");
        var tasksPath = Path.Combine(WorkspacePath, ".aos", "spec", "tasks");

        try
        {
            // Load from uat directory structure
            if (Directory.Exists(uatPath))
            {
                var uatFiles = Directory.GetFiles(uatPath, "*.json");
                foreach (var uatFile in uatFiles)
                {
                    var session = ParseUatFile(uatFile, tasksPath);
                    if (session != null)
                    {
                        UatSessions.Add(session);
                    }
                }

                // Also check for uat.json files in task directories
                if (Directory.Exists(tasksPath))
                {
                    var taskDirs = Directory.GetDirectories(tasksPath);
                    foreach (var taskDir in taskDirs)
                    {
                        var uatJsonPath = Path.Combine(taskDir, "uat.json");
                        if (System.IO.File.Exists(uatJsonPath))
                        {
                            // Check if this task already has a session loaded
                            var taskId = Path.GetFileName(taskDir);
                            if (!UatSessions.Any(s => s.TaskId == taskId))
                            {
                                var session = ParseTaskUatFile(taskId, uatJsonPath, taskDir);
                                if (session != null)
                                {
                                    UatSessions.Add(session);
                                }
                            }
                        }
                    }
                }
            }
            else if (Directory.Exists(tasksPath))
            {
                // If no uat directory, check task directories for uat.json
                var taskDirs = Directory.GetDirectories(tasksPath);
                foreach (var taskDir in taskDirs)
                {
                    var uatJsonPath = Path.Combine(taskDir, "uat.json");
                    if (System.IO.File.Exists(uatJsonPath))
                    {
                        var taskId = Path.GetFileName(taskDir);
                        var session = ParseTaskUatFile(taskId, uatJsonPath, taskDir);
                        if (session != null)
                        {
                            UatSessions.Add(session);
                        }
                    }
                }
            }

            // Sort by started date descending
            UatSessions = UatSessions.OrderByDescending(s => s.StartedAt).ToList();

            // Apply filters
            ApplyFilters();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading UAT sessions");
            ErrorMessage = $"Error loading UAT sessions: {ex.Message}";
        }
    }

    private UatSessionViewModel? ParseUatFile(string uatFile, string tasksPath)
    {
        try
        {
            var json = System.IO.File.ReadAllText(uatFile);
            var uatDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (uatDoc == null) return null;

            var root = uatDoc.RootElement;
            var sessionId = Path.GetFileNameWithoutExtension(uatFile);

            var session = new UatSessionViewModel
            {
                Id = sessionId,
                TaskId = root.TryGetProperty("taskId", out var taskId) ? taskId.GetString() ?? "" : "",
                TaskName = "Unknown Task",
                StartedAt = root.TryGetProperty("startedAt", out var startedAt) && 
                            startedAt.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(startedAt.GetString(), out var startDate) 
                            ? startDate : DateTime.Now,
                CompletedAt = root.TryGetProperty("completedAt", out var completedAt) && 
                              completedAt.ValueKind == JsonValueKind.String &&
                              DateTime.TryParse(completedAt.GetString(), out var completeDate) 
                              ? completeDate : null,
                Checks = new List<UatCheckViewModel>()
            };

            // Load task name
            if (!string.IsNullOrEmpty(session.TaskId))
            {
                var taskFile = Path.Combine(tasksPath, session.TaskId, "task.json");
                if (System.IO.File.Exists(taskFile))
                {
                    try
                    {
                        var taskJson = System.IO.File.ReadAllText(taskFile);
                        var taskDoc = JsonSerializer.Deserialize<JsonDocument>(taskJson);
                        if (taskDoc?.RootElement.TryGetProperty("task", out var taskProp) == true &&
                            taskProp.TryGetProperty("title", out var title))
                        {
                            session.TaskName = title.GetString() ?? session.TaskId;
                        }
                        else if (taskDoc?.RootElement.TryGetProperty("title", out title) == true)
                        {
                            session.TaskName = title.GetString() ?? session.TaskId;
                        }
                    }
                    catch
                    {
                        session.TaskName = session.TaskId;
                    }
                }
            }

            // Parse checks
            if (root.TryGetProperty("checks", out var checks))
            {
                foreach (var check in checks.EnumerateArray())
                {
                    var checkItem = new UatCheckViewModel
                    {
                        Id = check.TryGetProperty("id", out var checkId) ? checkId.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                        Description = check.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        Passed = check.TryGetProperty("passed", out var passed) ? passed.GetBoolean() : null,
                        ReproNotes = check.TryGetProperty("reproNotes", out var notes) ? notes.GetString() : null,
                        VerifiedAt = check.TryGetProperty("verifiedAt", out var verifiedAt) && 
                                     verifiedAt.ValueKind == JsonValueKind.String &&
                                     DateTime.TryParse(verifiedAt.GetString(), out var verifyDate) 
                                     ? verifyDate : null,
                        VerifiedBy = check.TryGetProperty("verifiedBy", out var verifiedBy) ? verifiedBy.GetString() : null,
                        IssueId = check.TryGetProperty("issueId", out var issueId) ? issueId.GetString() : null
                    };
                    session.Checks.Add(checkItem);
                }
            }

            // Calculate counts
            session.PassedCount = session.Checks.Count(c => c.Passed == true);
            session.FailedCount = session.Checks.Count(c => c.Passed == false);
            session.PendingCount = session.Checks.Count(c => c.Passed == null);

            // Load related issues and latest run
            LoadRelatedData(session);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse UAT file {UatFile}", uatFile);
            return null;
        }
    }

    private UatSessionViewModel? ParseTaskUatFile(string taskId, string uatJsonPath, string taskDir)
    {
        try
        {
            var json = System.IO.File.ReadAllText(uatJsonPath);
            var uatDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (uatDoc == null) return null;

            var root = uatDoc.RootElement;
            var taskName = taskId;

            // Try to get task name from task.json
            var taskFile = Path.Combine(taskDir, "task.json");
            if (System.IO.File.Exists(taskFile))
            {
                try
                {
                    var taskJson = System.IO.File.ReadAllText(taskFile);
                    var taskDoc = JsonSerializer.Deserialize<JsonDocument>(taskJson);
                    if (taskDoc?.RootElement.TryGetProperty("task", out var taskProp) == true &&
                        taskProp.TryGetProperty("title", out var title))
                    {
                        taskName = title.GetString() ?? taskId;
                    }
                    else if (taskDoc?.RootElement.TryGetProperty("title", out title) == true)
                    {
                        taskName = title.GetString() ?? taskId;
                    }
                }
                catch { }
            }

            var session = new UatSessionViewModel
            {
                Id = $"uat-{taskId}",
                TaskId = taskId,
                TaskName = taskName,
                StartedAt = root.TryGetProperty("startedAt", out var startedAt) && 
                          startedAt.ValueKind == JsonValueKind.String &&
                          DateTime.TryParse(startedAt.GetString(), out var startDate) 
                          ? startDate : DateTime.Now,
                CompletedAt = root.TryGetProperty("completedAt", out var completedAt) && 
                              completedAt.ValueKind == JsonValueKind.String &&
                              DateTime.TryParse(completedAt.GetString(), out var completeDate) 
                              ? completeDate : null,
                Checks = new List<UatCheckViewModel>()
            };

            // Parse checks/acceptance criteria
            if (root.TryGetProperty("checks", out var checks))
            {
                foreach (var check in checks.EnumerateArray())
                {
                    var checkItem = new UatCheckViewModel
                    {
                        Id = check.TryGetProperty("id", out var checkId) ? checkId.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                        Description = check.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "",
                        Passed = check.TryGetProperty("passed", out var passed) ? passed.GetBoolean() : null,
                        ReproNotes = check.TryGetProperty("reproNotes", out var notes) ? notes.GetString() : null,
                        VerifiedAt = check.TryGetProperty("verifiedAt", out var verifiedAt) && 
                                     verifiedAt.ValueKind == JsonValueKind.String &&
                                     DateTime.TryParse(verifiedAt.GetString(), out var verifyDate) 
                                     ? verifyDate : null,
                        VerifiedBy = check.TryGetProperty("verifiedBy", out var verifiedBy) ? verifiedBy.GetString() : null,
                        IssueId = check.TryGetProperty("issueId", out var issueId) ? issueId.GetString() : null
                    };
                    session.Checks.Add(checkItem);
                }
            }
            else if (root.TryGetProperty("acceptanceCriteria", out var criteria))
            {
                // Build checks from acceptance criteria if no checks defined
                int idx = 0;
                foreach (var criterion in criteria.EnumerateArray())
                {
                    var desc = criterion.GetString() ?? "";
                    if (!string.IsNullOrEmpty(desc))
                    {
                        session.Checks.Add(new UatCheckViewModel
                        {
                            Id = $"ac-{idx++}",
                            Description = desc,
                            Passed = null
                        });
                    }
                }
            }

            // Calculate counts
            session.PassedCount = session.Checks.Count(c => c.Passed == true);
            session.FailedCount = session.Checks.Count(c => c.Passed == false);
            session.PendingCount = session.Checks.Count(c => c.Passed == null);

            // Load related issues and latest run
            LoadRelatedData(session);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse task UAT file {UatFile}", uatJsonPath);
            return null;
        }
    }

    private void ApplyFilters()
    {
        if (!string.IsNullOrEmpty(FilterStatus))
        {
            UatSessions = UatSessions.Where(s =>
            {
                var status = s.FailedCount > 0 ? "Failed" :
                             s.PendingCount == 0 && s.PassedCount > 0 ? "Passed" :
                             s.PassedCount > 0 ? "InProgress" : "Pending";
                return status.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        if (!string.IsNullOrEmpty(FilterTaskId))
        {
            UatSessions = UatSessions.Where(s => s.TaskId == FilterTaskId).ToList();
        }

        if (!string.IsNullOrEmpty(SearchQuery))
        {
            var query = SearchQuery.ToLowerInvariant();
            UatSessions = UatSessions.Where(s =>
                s.Id.ToLowerInvariant().Contains(query) ||
                s.TaskId.ToLowerInvariant().Contains(query) ||
                s.TaskName.ToLowerInvariant().Contains(query)
            ).ToList();
        }
    }

    private void LoadRelatedData(UatSessionViewModel session)
    {
        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(session.TaskId))
        {
            return;
        }

        // Load related issues from issue IDs in checks
        session.RelatedIssueIds = session.Checks
            .Where(c => !string.IsNullOrEmpty(c.IssueId))
            .Select(c => c.IssueId!)
            .Distinct()
            .ToList();

        // Load latest run ID from state
        try
        {
            var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");
            if (System.IO.File.Exists(statePath))
            {
                var stateJson = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);

                if (stateDoc?.RootElement.TryGetProperty("tasks", out var tasks) == true &&
                    tasks.TryGetProperty(session.TaskId, out var taskState))
                {
                    if (taskState.TryGetProperty("latestRunId", out var runId))
                    {
                        session.LatestRunId = runId.GetString();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load related run data for task {TaskId}", session.TaskId);
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
