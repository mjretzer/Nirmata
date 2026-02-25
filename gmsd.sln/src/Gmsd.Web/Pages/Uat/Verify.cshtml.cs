using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;
using Gmsd.Web.AgentRunner;
using Gmsd.Web.Services;

namespace Gmsd.Web.Pages.Uat;

public class VerifyModel : PageModel
{
    private readonly ILogger<VerifyModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly WorkflowClassifier? _agentRunner;

    public VerifyModel(ILogger<VerifyModel> logger, IConfiguration configuration, WorkflowClassifier? agentRunner = null)
    {
        _logger = logger;
        _configuration = configuration;
        _agentRunner = agentRunner;
    }

    [BindProperty(SupportsGet = true)]
    public string TaskId { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? SessionId { get; set; }

    public string TaskName { get; set; } = "Unknown Task";
    public string SessionIdDisplay => SessionId ?? $"uat-{TaskId}";
    public List<UatCheckViewModel> Checks { get; set; } = new();
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? LatestRunId { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public int PassedCount => Checks.Count(c => c.Passed == true);
    public int FailedCount => Checks.Count(c => c.Passed == false);
    public int PendingCount => Checks.Count(c => c.Passed == null);
    public int TotalCount => Checks.Count;
    public int ProgressPercent => TotalCount > 0 ? ((PassedCount + FailedCount) * 100) / TotalCount : 0;

    public string OverallStatus => FailedCount > 0 ? "Failed" :
                                   PendingCount == 0 && PassedCount > 0 ? "Passed" :
                                   PassedCount > 0 ? "InProgress" : "Pending";

    public DiagnosticArtifactViewModel? UatDiagnostic { get; set; }
    public ArtifactValidationStatusViewModel? UatValidationStatus { get; set; }

    private string? WorkspacePath { get; set; }

    public void OnGet()
    {
        if (string.IsNullOrEmpty(TaskId))
        {
            ErrorMessage = "No task ID provided.";
            return;
        }

        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        LoadTaskInfo();
        LoadVerificationSession();
    }

    public IActionResult OnPostPassCheck(string checkId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return Page();
        }

        LoadVerificationSession();
        var check = Checks.FirstOrDefault(c => c.Id == checkId);
        if (check != null)
        {
            check.Passed = true;
            check.VerifiedAt = DateTime.Now;
            check.VerifiedBy = "User";
            SaveVerificationSession();
            SuccessMessage = $"Check passed: {check.Description.Substring(0, Math.Min(50, check.Description.Length))}...";
        }

        LoadTaskInfo();
        return Page();
    }

    public IActionResult OnPostFailCheck(string checkId, [FromForm] Dictionary<string, string> formData)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return Page();
        }

        LoadVerificationSession();
        var check = Checks.FirstOrDefault(c => c.Id == checkId);
        if (check != null)
        {
            // Get repro notes from form
            var reproNotesKey = formData.Keys.FirstOrDefault(k => k.StartsWith("ReproNotes_"));
            var reproNotes = reproNotesKey != null ? formData[reproNotesKey] : null;

            check.Passed = false;
            check.VerifiedAt = DateTime.Now;
            check.VerifiedBy = "User";
            check.ReproNotes = reproNotes;

            // Create an issue for the failed check
            check.IssueId = CreateIssueForFailedCheck(check);

            SaveVerificationSession();
            SuccessMessage = $"Check failed and issue created: {check.IssueId}";
        }

        LoadTaskInfo();
        return Page();
    }

    public IActionResult OnPostSkipCheck(string checkId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return Page();
        }

        LoadVerificationSession();
        // Skip just moves to next - no state change needed
        SuccessMessage = "Check skipped.";

        LoadTaskInfo();
        return Page();
    }

    public IActionResult OnPostResetCheck(string checkId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return Page();
        }

        LoadVerificationSession();
        var check = Checks.FirstOrDefault(c => c.Id == checkId);
        if (check != null)
        {
            check.Passed = null;
            check.VerifiedAt = null;
            check.VerifiedBy = null;
            check.ReproNotes = null;
            // Keep issue ID for reference but could clear it if needed
            SaveVerificationSession();
            SuccessMessage = "Check reset.";
        }

        LoadTaskInfo();
        return Page();
    }

    public IActionResult OnPostSaveProgress()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return Page();
        }

        SaveVerificationSession();
        LoadTaskInfo();
        SuccessMessage = "Progress saved.";
        return Page();
    }

    public IActionResult OnPostCompleteVerification()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return Page();
        }

        LoadVerificationSession();
        if (FailedCount > 0)
        {
            ErrorMessage = "Cannot complete verification with failed checks. Please fix issues first.";
            LoadTaskInfo();
            return Page();
        }

        if (PendingCount > 0)
        {
            ErrorMessage = "Cannot complete verification with pending checks.";
            LoadTaskInfo();
            return Page();
        }

        CompletedAt = DateTime.Now;
        SaveVerificationSession();

        // Update task status to Verified
        UpdateTaskStatus("Verified");

        return RedirectToPage("Index", new { SuccessMessage = "Verification completed successfully!" });
    }

    public IActionResult OnPostRestartVerification()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return Page();
        }

        // Reset all checks
        foreach (var check in Checks)
        {
            check.Passed = null;
            check.VerifiedAt = null;
            check.VerifiedBy = null;
            check.ReproNotes = null;
            check.IssueId = null;
        }

        StartedAt = DateTime.Now;
        CompletedAt = null;
        SaveVerificationSession();

        LoadTaskInfo();
        SuccessMessage = "Verification restarted.";
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

    private void LoadTaskInfo()
    {
        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(TaskId))
        {
            return;
        }

        var taskFile = Path.Combine(WorkspacePath, ".aos", "spec", "tasks", TaskId, "task.json");
        if (System.IO.File.Exists(taskFile))
        {
            try
            {
                var json = System.IO.File.ReadAllText(taskFile);
                var taskDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                if (taskDoc?.RootElement.TryGetProperty("task", out var taskProp) == true)
                {
                    if (taskProp.TryGetProperty("title", out var title))
                    {
                        TaskName = title.GetString() ?? TaskId;
                    }
                }
                else if (taskDoc?.RootElement.TryGetProperty("title", out var rootTitle) == true)
                {
                    TaskName = rootTitle.GetString() ?? TaskId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load task info for {TaskId}", TaskId);
            }
        }

        // Load UAT diagnostic
        var uatFile = Path.Combine(WorkspacePath, ".aos", "spec", "tasks", TaskId, "uat.json");
        LoadDiagnosticForArtifact(uatFile, "spec/tasks", TaskId, out var uatDiag, out var uatStatus);
        UatDiagnostic = uatDiag;
        UatValidationStatus = uatStatus;

        // Load latest run ID from state
        var statePath = Path.Combine(WorkspacePath, ".aos", "state", "state.json");
        if (System.IO.File.Exists(statePath))
        {
            try
            {
                var stateJson = System.IO.File.ReadAllText(statePath);
                var stateDoc = JsonSerializer.Deserialize<JsonDocument>(stateJson);

                if (stateDoc?.RootElement.TryGetProperty("tasks", out var tasks) == true &&
                    tasks.TryGetProperty(TaskId, out var taskState))
                {
                    if (taskState.TryGetProperty("latestRunId", out var runId))
                    {
                        LatestRunId = runId.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load state for task {TaskId}", TaskId);
            }
        }
    }

    private void LoadVerificationSession()
    {
        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(TaskId))
        {
            return;
        }

        // First, load acceptance criteria from task
        var taskFile = Path.Combine(WorkspacePath, ".aos", "spec", "tasks", TaskId, "task.json");
        var acceptanceCriteria = new List<string>();

        if (System.IO.File.Exists(taskFile))
        {
            try
            {
                var json = System.IO.File.ReadAllText(taskFile);
                var taskDoc = JsonSerializer.Deserialize<JsonDocument>(json);

                JsonElement taskElement;
                if (taskDoc?.RootElement.TryGetProperty("task", out var taskProp) == true)
                {
                    taskElement = taskProp;
                }
                else
                {
                    taskElement = taskDoc?.RootElement ?? default;
                }

                if (taskElement.TryGetProperty("acceptanceCriteria", out var criteria))
                {
                    foreach (var criterion in criteria.EnumerateArray())
                    {
                        var desc = criterion.GetString();
                        if (!string.IsNullOrEmpty(desc))
                        {
                            acceptanceCriteria.Add(desc);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load acceptance criteria for task {TaskId}", TaskId);
            }
        }

        // Try to load existing session data
        var uatDir = Path.Combine(WorkspacePath, ".aos", "spec", "uat");
        var uatFile = Path.Combine(uatDir, $"{SessionIdDisplay}.json");
        var taskUatFile = Path.Combine(WorkspacePath, ".aos", "spec", "tasks", TaskId, "uat.json");

        bool hasExistingSession = false;

        if (System.IO.File.Exists(uatFile))
        {
            try
            {
                var json = System.IO.File.ReadAllText(uatFile);
                var uatDoc = JsonSerializer.Deserialize<JsonDocument>(json);
                ParseSessionData(uatDoc);
                hasExistingSession = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load UAT session from {UatFile}", uatFile);
            }
        }
        else if (System.IO.File.Exists(taskUatFile))
        {
            try
            {
                var json = System.IO.File.ReadAllText(taskUatFile);
                var uatDoc = JsonSerializer.Deserialize<JsonDocument>(json);
                ParseSessionData(uatDoc);
                hasExistingSession = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load UAT data from {TaskUatFile}", taskUatFile);
            }
        }

        // If no existing session or no checks loaded, build from acceptance criteria
        if (!hasExistingSession || Checks.Count == 0)
        {
            Checks = acceptanceCriteria.Select((criterion, idx) => new UatCheckViewModel
            {
                Id = $"ac-{idx}",
                Description = criterion,
                Passed = null
            }).ToList();

            StartedAt = DateTime.Now;
        }

        // Ensure all acceptance criteria have corresponding checks
        for (int i = 0; i < acceptanceCriteria.Count; i++)
        {
            var criterion = acceptanceCriteria[i];
            if (!Checks.Any(c => c.Description == criterion))
            {
                Checks.Add(new UatCheckViewModel
                {
                    Id = $"ac-{i}",
                    Description = criterion,
                    Passed = null
                });
            }
        }
    }

    private void ParseSessionData(JsonDocument? uatDoc)
    {
        if (uatDoc == null) return;

        var root = uatDoc.RootElement;

        StartedAt = root.TryGetProperty("startedAt", out var startedAt) &&
                    startedAt.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(startedAt.GetString(), out var startDate)
                    ? startDate : DateTime.Now;

        CompletedAt = root.TryGetProperty("completedAt", out var completedAt) &&
                      completedAt.ValueKind == JsonValueKind.String &&
                      DateTime.TryParse(completedAt.GetString(), out var completeDate)
                      ? completeDate : null;

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
                Checks.Add(checkItem);
            }
        }
    }

    private void SaveVerificationSession()
    {
        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(TaskId))
        {
            return;
        }

        try
        {
            // Ensure uat directory exists
            var uatDir = Path.Combine(WorkspacePath, ".aos", "spec", "uat");
            Directory.CreateDirectory(uatDir);

            var sessionData = new
            {
                taskId = TaskId,
                sessionId = SessionIdDisplay,
                startedAt = StartedAt?.ToString("O"),
                completedAt = CompletedAt?.ToString("O"),
                checks = Checks.Select(c => new
                {
                    id = c.Id,
                    description = c.Description,
                    passed = c.Passed,
                    reproNotes = c.ReproNotes,
                    verifiedAt = c.VerifiedAt?.ToString("O"),
                    verifiedBy = c.VerifiedBy,
                    issueId = c.IssueId
                }).ToList()
            };

            var json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });
            var uatFile = Path.Combine(uatDir, $"{SessionIdDisplay}.json");
            System.IO.File.WriteAllText(uatFile, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save verification session");
            ErrorMessage = "Failed to save verification session.";
        }
    }

    private string CreateIssueForFailedCheck(UatCheckViewModel check)
    {
        try
        {
            var issuesDir = Path.Combine(WorkspacePath!, ".aos", "spec", "issues");
            Directory.CreateDirectory(issuesDir);

            var issueId = $"issue-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";
            var issueFile = Path.Combine(issuesDir, $"{issueId}.json");

            var issueData = new
            {
                issue = new
                {
                    id = issueId,
                    title = $"UAT Failed: {check.Description.Substring(0, Math.Min(50, check.Description.Length))}...",
                    description = $"Acceptance criteria failed during UAT verification for task {TaskId}.\n\nCriteria: {check.Description}",
                    type = "Bug",
                    severity = "High",
                    status = "Open",
                    taskId = TaskId,
                    taskName = TaskName,
                    uatSessionId = SessionIdDisplay,
                    uatCheckId = check.Id,
                    reproSteps = check.ReproNotes ?? "No reproduction steps provided.",
                    expectedBehavior = check.Description,
                    actualBehavior = "Failed verification - see repro notes",
                    createdAt = DateTime.Now.ToString("O"),
                    updatedAt = DateTime.Now.ToString("O")
                }
            };

            var json = JsonSerializer.Serialize(issueData, new JsonSerializerOptions { WriteIndented = true });
            System.IO.File.WriteAllText(issueFile, json);

            _logger.LogInformation("Created issue {IssueId} for failed UAT check", issueId);
            return issueId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create issue for failed check");
            return "error-creating-issue";
        }
    }

    private void UpdateTaskStatus(string status)
    {
        try
        {
            var taskFile = Path.Combine(WorkspacePath!, ".aos", "spec", "tasks", TaskId, "task.json");
            if (!System.IO.File.Exists(taskFile))
            {
                return;
            }

            var json = System.IO.File.ReadAllText(taskFile);
            var doc = JsonDocument.Parse(json);

            // Create updated document with new status
            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            bool hasTaskProperty = doc.RootElement.TryGetProperty("task", out _);

            if (hasTaskProperty)
            {
                writer.WritePropertyName("task");
                writer.WriteStartObject();
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.NameEquals("task") && hasTaskProperty)
                {
                    foreach (var taskProp in property.Value.EnumerateObject())
                    {
                        if (taskProp.NameEquals("status"))
                        {
                            writer.WriteString("status", status);
                        }
                        else
                        {
                            taskProp.WriteTo(writer);
                        }
                    }
                    writer.WriteString("status", status); // Ensure status is set
                    writer.WriteString("verifiedAt", DateTime.Now.ToString("O"));
                }
                else if (property.NameEquals("status"))
                {
                    writer.WriteString("status", status);
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            if (!hasTaskProperty && !doc.RootElement.TryGetProperty("status", out _))
            {
                writer.WriteString("status", status);
                writer.WriteString("verifiedAt", DateTime.Now.ToString("O"));
            }

            if (hasTaskProperty)
            {
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();

            var updatedJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            System.IO.File.WriteAllText(taskFile, updatedJson);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update task status");
        }
    }

    private void LoadDiagnosticForArtifact(
        string artifactPath,
        string artifactType,
        string artifactId,
        out DiagnosticArtifactViewModel? diagnostic,
        out ArtifactValidationStatusViewModel? validationStatus)
    {
        diagnostic = null;
        validationStatus = null;
        try
        {
            if (string.IsNullOrEmpty(WorkspacePath))
            {
                return;
            }

            var fileName = Path.GetFileNameWithoutExtension(artifactPath);
            var diagnosticPath = Path.Combine(WorkspacePath, ".aos", "diagnostics", artifactType, $"{fileName}.diagnostic.json");

            if (System.IO.File.Exists(diagnosticPath))
            {
                var json = System.IO.File.ReadAllText(diagnosticPath);
                diagnostic = JsonSerializer.Deserialize<DiagnosticArtifactViewModel>(json);

                if (diagnostic != null)
                {
                    validationStatus = new ArtifactValidationStatusViewModel
                    {
                        IsValid = false,
                        ValidationMessage = $"Validation failed: {diagnostic.ValidationErrors.Count} error(s) found",
                        Diagnostic = diagnostic,
                        ValidatedAt = diagnostic.Timestamp.UtcDateTime
                    };
                }
            }
            else
            {
                validationStatus = new ArtifactValidationStatusViewModel
                {
                    IsValid = true,
                    ValidationMessage = "Artifact is valid",
                    ValidatedAt = DateTime.UtcNow
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load diagnostic for artifact {ArtifactPath}", artifactPath);
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
