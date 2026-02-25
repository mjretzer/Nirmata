using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Gmsd.Web.Models;
using Gmsd.Web.AgentRunner;

namespace Gmsd.Web.Pages.Issues;

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

    public IssueViewModel? Issue { get; set; }
    public string? WorkspacePath { get; set; }
    public string? NotFoundMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public string? NewStatus { get; set; }

    [BindProperty]
    public string? ResolutionNotes { get; set; }

    public void OnGet(string issueId)
    {
        if (string.IsNullOrEmpty(issueId))
        {
            NotFoundMessage = "No issue ID specified.";
            return;
        }

        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        LoadIssue(issueId);
    }

    public async Task<IActionResult> OnPostRouteToFixPlanAsync(string issueId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return Page();
        }

        LoadIssue(issueId);
        if (Issue == null)
        {
            NotFoundMessage = $"Issue '{issueId}' was not found.";
            return Page();
        }

        try
        {
            var result = await CreateFixPlanAsync(issueId);
            if (result.success)
            {
                SuccessMessage = $"Fix plan created and routed to task '{result.taskId}'.";
                Issue.Status = IssueStatus.InProgress;
                PersistIssueStatus(issueId, IssueStatus.InProgress);
            }
            else
            {
                ErrorMessage = result.errorMessage ?? "Failed to create fix plan.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing issue {IssueId} to fix plan", issueId);
            ErrorMessage = $"Error creating fix plan: {ex.Message}";
        }

        return Page();
    }

    public IActionResult OnPostMarkStatus(string issueId)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected.";
            return Page();
        }

        LoadIssue(issueId);
        if (Issue == null)
        {
            NotFoundMessage = $"Issue '{issueId}' was not found.";
            return Page();
        }

        if (string.IsNullOrEmpty(NewStatus))
        {
            ErrorMessage = "Status is required.";
            return Page();
        }

        try
        {
            if (Enum.TryParse<IssueStatus>(NewStatus, out var newStatusValue))
            {
                var result = PersistIssueStatus(issueId, newStatusValue, ResolutionNotes);
                if (result)
                {
                    SuccessMessage = $"Issue status updated to {NewStatus}.";
                    Issue.Status = newStatusValue;
                    if (newStatusValue == IssueStatus.Resolved || newStatusValue == IssueStatus.Closed)
                    {
                        Issue.ResolvedAt = DateTime.UtcNow;
                        Issue.ResolutionNotes = ResolutionNotes;
                    }
                }
                else
                {
                    ErrorMessage = "Failed to update issue status.";
                }
            }
            else
            {
                ErrorMessage = $"Invalid status: {NewStatus}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking issue status for {IssueId}", issueId);
            ErrorMessage = $"Error updating status: {ex.Message}";
        }

        NewStatus = null;
        ResolutionNotes = null;
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
                NotFoundMessage = "No workspace selected. Please select a workspace first.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load workspace configuration");
            NotFoundMessage = "Failed to load workspace configuration.";
        }
    }

    private void LoadIssue(string issueId)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var issueFile = Path.Combine(WorkspacePath, ".aos", "spec", "issues", $"{issueId}.json");

        if (!System.IO.File.Exists(issueFile))
        {
            NotFoundMessage = $"Issue '{issueId}' was not found.";
            return;
        }

        try
        {
            var json = System.IO.File.ReadAllText(issueFile);
            var issueDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (issueDoc == null)
            {
                NotFoundMessage = "Failed to parse issue file.";
                return;
            }

            JsonElement root;
            if (issueDoc.RootElement.TryGetProperty("issue", out var issueProp))
            {
                root = issueProp;
            }
            else
            {
                root = issueDoc.RootElement;
            }

            Issue = new IssueViewModel
            {
                Id = issueId,
                Title = root.TryGetProperty("title", out var title) ? title.GetString() ?? issueId : issueId,
                Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                Type = root.TryGetProperty("type", out var type) &&
                       Enum.TryParse<IssueType>(type.GetString(), out var parsedType)
                       ? parsedType : IssueType.Bug,
                Severity = root.TryGetProperty("severity", out var severity) &&
                           Enum.TryParse<IssueSeverity>(severity.GetString(), out var parsedSeverity)
                           ? parsedSeverity : IssueSeverity.Medium,
                Status = root.TryGetProperty("status", out var status) &&
                         Enum.TryParse<IssueStatus>(status.GetString(), out var parsedStatus)
                         ? parsedStatus : IssueStatus.Open,
                ReproSteps = root.TryGetProperty("reproSteps", out var repro) ? repro.GetString() : null,
                ExpectedBehavior = root.TryGetProperty("expectedBehavior", out var expected) ? expected.GetString() : null,
                ActualBehavior = root.TryGetProperty("actualBehavior", out var actual) ? actual.GetString() : null,
                TaskId = root.TryGetProperty("taskId", out var taskId) ? taskId.GetString() : null,
                TaskName = root.TryGetProperty("taskName", out var taskName) ? taskName.GetString() : null,
                PhaseId = root.TryGetProperty("phaseId", out var phaseId) ? phaseId.GetString() : null,
                PhaseName = root.TryGetProperty("phaseName", out var phaseName) ? phaseName.GetString() : null,
                MilestoneId = root.TryGetProperty("milestoneId", out var milestoneId) ? milestoneId.GetString() : null,
                MilestoneName = root.TryGetProperty("milestoneName", out var milestoneName) ? milestoneName.GetString() : null,
                UatSessionId = root.TryGetProperty("uatSessionId", out var uatSession) ? uatSession.GetString() : null,
                UatCheckId = root.TryGetProperty("uatCheckId", out var uatCheck) ? uatCheck.GetString() : null,
                AssignedTo = root.TryGetProperty("assignedTo", out var assigned) ? assigned.GetString() : null,
                CreatedAt = root.TryGetProperty("createdAt", out var created) &&
                            created.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(created.GetString(), out var createdDate)
                            ? createdDate : null,
                UpdatedAt = root.TryGetProperty("updatedAt", out var updated) &&
                            updated.ValueKind == JsonValueKind.String &&
                            DateTime.TryParse(updated.GetString(), out var updatedDate)
                            ? updatedDate : null,
                ResolvedAt = root.TryGetProperty("resolvedAt", out var resolved) &&
                             resolved.ValueKind == JsonValueKind.String &&
                             DateTime.TryParse(resolved.GetString(), out var resolvedDate)
                             ? resolvedDate : null,
                ResolutionNotes = root.TryGetProperty("resolutionNotes", out var notes) ? notes.GetString() : null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load issue {IssueId}", issueId);
            NotFoundMessage = $"Error loading issue: {ex.Message}";
        }
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }

    private async Task<(bool success, string? taskId, string? errorMessage)> CreateFixPlanAsync(string issueId)
    {
        if (string.IsNullOrEmpty(WorkspacePath) || Issue == null)
        {
            return (false, null, "Workspace or issue not loaded.");
        }

        try
        {
            if (_agentRunner == null)
            {
                return (false, null, "Agent runner is not available.");
            }

                        // Use the WorkflowClassifier to create fix plan through the orchestrator
            var command = $"spec fix --issue-id {issueId}";
                        var result = await _agentRunner.ExecuteAsync(command);

            if (result.IsSuccess)
            {
                // Extract task ID from result output or generate one
                var fixTaskId = $"FIX-{issueId}-{DateTime.UtcNow:yyyyMMdd}";
                _logger.LogInformation("Created fix plan for issue {IssueId} via orchestrator", issueId);
                return (true, fixTaskId, null);
            }
            else
            {
                _logger.LogWarning("Fix plan creation failed for issue {IssueId}: {FinalPhase}", issueId, result.FinalPhase);
                return (false, null, $"Fix plan creation failed at phase: {result.FinalPhase}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating fix plan for issue {IssueId}", issueId);
            return (false, null, ex.Message);
        }
    }

    private bool PersistIssueStatus(string issueId, IssueStatus newStatus, string? resolutionNotes = null)
    {
        if (string.IsNullOrEmpty(WorkspacePath)) return false;

        try
        {
            var issueFile = Path.Combine(WorkspacePath, ".aos", "spec", "issues", $"{issueId}.json");

            if (!System.IO.File.Exists(issueFile))
            {
                return false;
            }

            // Read existing issue
            var json = System.IO.File.ReadAllText(issueFile);
            var issueDoc = JsonSerializer.Deserialize<JsonDocument>(json);

            if (issueDoc == null)
            {
                return false;
            }

            // Determine root element
            JsonElement root;
            if (issueDoc.RootElement.TryGetProperty("issue", out var issueProp))
            {
                root = issueProp;
            }
            else
            {
                root = issueDoc.RootElement;
            }

            // Write updated issue
            using var stream = new System.IO.MemoryStream();
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WritePropertyName("issue");
            writer.WriteStartObject();

            // Copy all existing properties except status, updatedAt, resolvedAt, resolutionNotes
            foreach (var property in root.EnumerateObject())
            {
                if (property.Name != "status" && property.Name != "updatedAt" &&
                    property.Name != "resolvedAt" && property.Name != "resolutionNotes")
                {
                    writer.WritePropertyName(property.Name);
                    property.Value.WriteTo(writer);
                }
            }

            // Write new status
            writer.WriteString("status", newStatus.ToString());
            writer.WriteString("updatedAt", DateTime.UtcNow.ToString("O"));

            // Write resolution info if applicable
            if (newStatus == IssueStatus.Resolved || newStatus == IssueStatus.Closed)
            {
                writer.WriteString("resolvedAt", DateTime.UtcNow.ToString("O"));
                if (!string.IsNullOrEmpty(resolutionNotes))
                {
                    writer.WriteString("resolutionNotes", resolutionNotes);
                }
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();

            var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
            System.IO.File.WriteAllText(issueFile, newJson);

            _logger.LogInformation("Updated issue {IssueId} status to {Status}", issueId, newStatus);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting issue status for {IssueId}", issueId);
            return false;
        }
    }

    private class WorkspaceConfig
    {
        public string? SelectedWorkspacePath { get; set; }
    }
}
