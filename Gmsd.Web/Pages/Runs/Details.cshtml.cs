using Gmsd.Agents.Models.Results;
using Gmsd.Agents.Persistence.State;
using Gmsd.Aos.Public;
using Gmsd.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Gmsd.Web.Pages.Runs;

/// <summary>
/// Page model for displaying run details including metadata, logs, and artifacts.
/// </summary>
public class DetailsModel : PageModel
{
    private readonly IRunRepository _runRepository;
    private readonly IWorkspace _workspace;
    private readonly ILogger<DetailsModel> _logger;

    public DetailsModel(
        IRunRepository runRepository,
        IWorkspace workspace,
        ILogger<DetailsModel> logger)
    {
        _runRepository = runRepository ?? throw new ArgumentNullException(nameof(runRepository));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// The run response data containing metadata.
    /// </summary>
    public RunResponse? Run { get; set; }

    /// <summary>
    /// Log entries read from the evidence folder.
    /// </summary>
    public List<LogEntry> Logs { get; set; } = new();

    /// <summary>
    /// Path to the summary.json artifact.
    /// </summary>
    public string? SummaryArtifactPath { get; set; }

    /// <summary>
    /// Path to the commands.json artifact.
    /// </summary>
    public string? CommandsArtifactPath { get; set; }

    /// <summary>
    /// Error message if the run was not found.
    /// </summary>
    public string? NotFoundMessage { get; set; }

    /// <summary>
    /// Related UAT sessions for this run.
    /// </summary>
    public List<RelatedUatViewModel> RelatedUatSessions { get; set; } = new();

    /// <summary>
    /// Related issues for this run.
    /// </summary>
    public List<IssueViewModel> RelatedIssues { get; set; } = new();

    /// <summary>
    /// The task ID associated with this run, if any.
    /// </summary>
    public string? RelatedTaskId { get; set; }

    public async Task<IActionResult> OnGetAsync(string runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            NotFoundMessage = "No run ID specified.";
            Run = null;
            return Page();
        }

        // Check if run exists in repository
        var run = await _runRepository.GetAsync(runId);
        if (run == null)
        {
            _logger.LogWarning("Run not found: {RunId}", runId);
            NotFoundMessage = $"Run '{runId}' was not found.";
            Run = null;
            return Page();
        }

        Run = run;

        // Build artifact paths
        var evidenceFolder = Path.Combine(_workspace.AosRootPath, "evidence", "runs", runId);
        SummaryArtifactPath = Path.Combine(evidenceFolder, "summary.json");
        CommandsArtifactPath = Path.Combine(evidenceFolder, "commands.json");

        // Load logs from the logs folder
        var logsFolder = Path.Combine(evidenceFolder, "logs");
        if (Directory.Exists(logsFolder))
        {
            try
            {
                var logFiles = Directory.GetFiles(logsFolder, "*.json", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f)
                    .ToList();

                foreach (var logFile in logFiles)
                {
                    try
                    {
                        var json = await System.IO.File.ReadAllTextAsync(logFile);
                        var doc = JsonDocument.Parse(json);
                        var entry = new LogEntry
                        {
                            FileName = Path.GetFileName(logFile),
                            Timestamp = GetJsonString(doc.RootElement, "timestamp"),
                            Level = GetJsonString(doc.RootElement, "level"),
                            Message = GetJsonString(doc.RootElement, "message"),
                            Source = GetJsonString(doc.RootElement, "source")
                        };
                        Logs.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse log file: {LogFile}", logFile);
                        Logs.Add(new LogEntry
                        {
                            FileName = Path.GetFileName(logFile),
                            Timestamp = null,
                            Level = "ERROR",
                            Message = $"Failed to parse log file: {ex.Message}",
                            Source = "LogParser"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read logs from {LogsFolder}", logsFolder);
            }
        }

        // Load related UAT sessions and issues
        LoadRelatedData(runId);

        return Page();
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return property.GetString();
        }
        return null;
    }

    private void LoadRelatedData(string runId)
    {
        try
        {
            var specPath = Path.Combine(_workspace.AosRootPath, "spec");
            var uatPath = Path.Combine(specPath, "uat");
            var issuesPath = Path.Combine(specPath, "issues");
            var tasksPath = Path.Combine(specPath, "tasks");

            // Load UAT sessions that reference this run
            if (Directory.Exists(uatPath))
            {
                var uatFiles = Directory.GetFiles(uatPath, "*.json");
                foreach (var uatFile in uatFiles)
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(uatFile);
                        var uatDoc = JsonSerializer.Deserialize<JsonDocument>(json);
                        if (uatDoc == null) continue;

                        var root = uatDoc.RootElement;
                        var sessionId = Path.GetFileNameWithoutExtension(uatFile);
                        var taskId = root.TryGetProperty("taskId", out var taskIdProp) ? taskIdProp.GetString() : null;

                        // Check if this UAT session has a latestRunId matching this run
                        // or if checks reference this run
                        bool isRelated = false;
                        if (root.TryGetProperty("latestRunId", out var latestRunId) &&
                            latestRunId.GetString() == runId)
                        {
                            isRelated = true;
                        }

                        // Also check state to see if this run is associated with the task
                        if (!isRelated && !string.IsNullOrEmpty(taskId))
                        {
                            var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
                            if (System.IO.File.Exists(statePath))
                            {
                                var stateJson = System.IO.File.ReadAllText(statePath);
                                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);
                                if (stateDoc?.RootElement.TryGetProperty("tasks", out var tasks) == true &&
                                    tasks.TryGetProperty(taskId, out var taskState))
                                {
                                    if (taskState.TryGetProperty("latestRunId", out var taskRunId) &&
                                        taskRunId.GetString() == runId)
                                    {
                                        isRelated = true;
                                        RelatedTaskId = taskId;
                                    }
                                }
                            }
                        }

                        if (isRelated)
                        {
                            var checks = new List<UatCheckViewModel>();
                            int passed = 0, failed = 0, pending = 0;

                            if (root.TryGetProperty("checks", out var checksElement))
                            {
                                foreach (var check in checksElement.EnumerateArray())
                                {
                                    var passedValue = check.TryGetProperty("passed", out var passedProp) ? passedProp.GetBoolean() : (bool?)null;
                                    if (passedValue == true) passed++;
                                    else if (passedValue == false) failed++;
                                    else pending++;
                                }
                            }

                            RelatedUatSessions.Add(new RelatedUatViewModel
                            {
                                SessionId = sessionId,
                                TaskId = taskId ?? "unknown",
                                TaskName = taskId ?? "Unknown Task",
                                Status = failed > 0 ? "Failed" : pending == 0 && passed > 0 ? "Passed" : "InProgress",
                                PassedCount = passed,
                                FailedCount = failed,
                                PendingCount = pending
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse UAT file {UatFile}", uatFile);
                    }
                }
            }

            // Load issues related to this run or related task
            if (Directory.Exists(issuesPath))
            {
                var issueFiles = Directory.GetFiles(issuesPath, "*.json");
                foreach (var issueFile in issueFiles)
                {
                    try
                    {
                        var json = System.IO.File.ReadAllText(issueFile);
                        var issueDoc = JsonSerializer.Deserialize<JsonDocument>(json);
                        if (issueDoc == null) continue;

                        JsonElement root;
                        if (issueDoc.RootElement.TryGetProperty("issue", out var issueProp))
                        {
                            root = issueProp;
                        }
                        else
                        {
                            root = issueDoc.RootElement;
                        }

                        var issueId = Path.GetFileNameWithoutExtension(issueFile);
                        var taskId = root.TryGetProperty("taskId", out var taskIdElement) ? taskIdElement.GetString() : null;

                        // Check if issue is related to this run's task or UAT
                        bool isRelated = false;
                        if (!string.IsNullOrEmpty(RelatedTaskId) && taskId == RelatedTaskId)
                        {
                            isRelated = true;
                        }

                        // Check if issue references any of the related UAT sessions
                        if (!isRelated)
                        {
                            var uatSessionId = root.TryGetProperty("uatSessionId", out var uatSession) ? uatSession.GetString() : null;
                            if (!string.IsNullOrEmpty(uatSessionId) &&
                                RelatedUatSessions.Any(u => u.SessionId == uatSessionId))
                            {
                                isRelated = true;
                            }
                        }

                        if (isRelated)
                        {
                            RelatedIssues.Add(new IssueViewModel
                            {
                                Id = issueId,
                                Title = root.TryGetProperty("title", out var title) ? title.GetString() ?? issueId : issueId,
                                Type = root.TryGetProperty("type", out var type) &&
                                       Enum.TryParse<IssueType>(type.GetString(), out var parsedType)
                                       ? parsedType : IssueType.Bug,
                                Severity = root.TryGetProperty("severity", out var severity) &&
                                           Enum.TryParse<IssueSeverity>(severity.GetString(), out var parsedSeverity)
                                           ? parsedSeverity : IssueSeverity.Medium,
                                Status = root.TryGetProperty("status", out var status) &&
                                         Enum.TryParse<IssueStatus>(status.GetString(), out var parsedStatus)
                                         ? parsedStatus : IssueStatus.Open,
                                TaskId = taskId,
                                TaskName = root.TryGetProperty("taskName", out var taskName) ? taskName.GetString() : null,
                                UatSessionId = root.TryGetProperty("uatSessionId", out var uatSessionId) ? uatSessionId.GetString() : null
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse issue file {IssueFile}", issueFile);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load related data for run {RunId}", runId);
        }
    }
}

/// <summary>
/// Represents a single log entry from the evidence logs folder.
/// </summary>
public class LogEntry
{
    /// <summary>
    /// The filename of the log entry.
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when the log entry was created.
    /// </summary>
    public string? Timestamp { get; set; }

    /// <summary>
    /// Log level (e.g., INFO, WARNING, ERROR).
    /// </summary>
    public string? Level { get; set; }

    /// <summary>
    /// The log message.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// The source/component that generated the log.
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// Represents a related UAT session for a run.
/// </summary>
public class RelatedUatViewModel
{
    public string SessionId { get; set; } = string.Empty;
    public string TaskId { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int PassedCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }
}
