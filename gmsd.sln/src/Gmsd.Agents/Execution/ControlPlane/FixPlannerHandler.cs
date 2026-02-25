using System.Text.Json;
using Gmsd.Agents.Execution.FixPlanner;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;

namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Command handler for the FixPlanner phase of the orchestrator workflow.
/// Coordinates fix planning when UAT verification fails, generating fix tasks
/// and routing to TaskExecutor for execution.
/// </summary>
public sealed class FixPlannerHandler
{
    private readonly IFixPlanner _fixPlanner;
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly ILogger<FixPlannerHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="FixPlannerHandler"/> class.
    /// </summary>
    public FixPlannerHandler(
        IFixPlanner fixPlanner,
        IWorkspace workspace,
        IStateStore stateStore,
        IRunLifecycleManager runLifecycleManager,
        ILogger<FixPlannerHandler> logger)
    {
        _fixPlanner = fixPlanner ?? throw new ArgumentNullException(nameof(fixPlanner));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the fix planner phase command.
    /// Reads issues from failed UAT verification and generates fix task plans.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result with routing to TaskExecutor.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Read current state to get parent task context
            var snapshot = _stateStore.ReadSnapshot();
            var parentTaskId = snapshot?.Cursor?.TaskId;

            if (string.IsNullOrEmpty(parentTaskId))
            {
                return CommandRouteResult.Failure(1, "No current task found in state cursor.");
            }

            // Extract issue IDs from the request or discover from issues directory
            var issueIds = await ExtractIssueIdsAsync(request, parentTaskId, ct);

            if (issueIds.Count == 0)
            {
                return CommandRouteResult.Failure(2, "No issues found for fix planning.");
            }

            // Extract context pack ID from request or generate new
            var contextPackId = ExtractContextPackId(request) ?? Guid.NewGuid().ToString("N");

            // Create fix planner request
            var fixPlannerRequest = new FixPlannerRequest
            {
                IssueIds = issueIds,
                WorkspaceRoot = _workspace.RepositoryRootPath,
                ParentTaskId = parentTaskId,
                ContextPackId = contextPackId
            };

            // Execute fix planning
            var fixPlannerResult = await _fixPlanner.PlanFixesAsync(fixPlannerRequest, ct);

            if (!fixPlannerResult.IsSuccess)
            {
                return CommandRouteResult.Failure(3, $"Fix planning failed: {fixPlannerResult.ErrorMessage}");
            }

            var structuredFixPlan = ParseValidatedStructuredFixPlan(fixPlannerResult, parentTaskId);

            _logger.LogInformation(
                "Fix planner produced structured fix plan for task {ParentTaskId} with {FixCount} fix entries.",
                parentTaskId,
                structuredFixPlan.Fixes.Count);

            // Record command completion
            await _runLifecycleManager.RecordCommandAsync(
                runId,
                "run",
                "fix-plan",
                "completed",
                ct);

            // Success - return with routing hint to TaskExecutor
            var fixTaskCount = fixPlannerResult.FixTaskIds.Count;
            var issueCount = fixPlannerResult.IssueAnalysis.Count;

            return new CommandRouteResult
            {
                IsSuccess = true,
                Output = $"Fix planning completed for {parentTaskId}. " +
                        $"Created {fixTaskCount} fix task(s) for {issueCount} issue(s). " +
                        $"First fix task: {fixPlannerResult.FixTaskIds.FirstOrDefault()}",
                RoutingHint = "TaskExecutor" // Signal to orchestrator to route to TaskExecutor
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fix planner handler failed for run {RunId}.", runId);
            return CommandRouteResult.Failure(99, $"Fix planner handler failed: {ex.Message}");
        }
    }

    private FixPlan ParseValidatedStructuredFixPlan(FixPlannerResult fixPlannerResult, string parentTaskId)
    {
        if (string.IsNullOrWhiteSpace(fixPlannerResult.StructuredFixPlanJson))
        {
            _logger.LogError(
                "Fix planner result for task {ParentTaskId} did not include structured fix plan JSON.",
                parentTaskId);
            throw new InvalidOperationException("Fix planner response did not include structured fix plan payload.");
        }

        // Validate fix plan schema using ArtifactContractValidator
        var fixPlanPath = Path.Combine(_workspace.AosRootPath, "spec", "tasks", parentTaskId, "fix-plan.json");
        var schemaValidation = ArtifactContractValidator.ValidateFixPlan(
            artifactPath: fixPlanPath,
            artifactJson: fixPlannerResult.StructuredFixPlanJson,
            aosRootPath: _workspace.AosRootPath,
            readBoundary: "fix-planner-handler");

        if (!schemaValidation.IsValid)
        {
            _logger.LogError(
                "Fix plan schema validation failed for task {ParentTaskId}: {ValidationMessage}. Diagnostic: {DiagnosticPath}",
                parentTaskId,
                schemaValidation.Message,
                schemaValidation.DiagnosticPath);
            throw new InvalidOperationException(
                $"Fix planner structured payload failed schema validation: {schemaValidation.Message}");
        }

        FixPlan? structuredFixPlan;
        try
        {
            structuredFixPlan = JsonSerializer.Deserialize<FixPlan>(fixPlannerResult.StructuredFixPlanJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Structured fix plan payload for task {ParentTaskId} was invalid JSON.",
                parentTaskId);
            throw new InvalidOperationException(
                $"Fix planner returned malformed structured JSON payload: {ex.Message}",
                ex);
        }

        if (structuredFixPlan is null)
        {
            _logger.LogError(
                "Structured fix plan payload for task {ParentTaskId} deserialized to null.",
                parentTaskId);
            throw new InvalidOperationException("Fix planner structured payload was empty or null.");
        }

        return structuredFixPlan;
    }

    /// <summary>
    /// Extracts issue IDs from the request or discovers them from the issues directory.
    /// </summary>
    private async Task<IReadOnlyList<string>> ExtractIssueIdsAsync(CommandRequest request, string parentTaskId, CancellationToken ct)
    {
        var issueIds = new List<string>();

        // Try to extract from request options
        if (request.Options.TryGetValue("issue-ids", out var issueIdsValue) && !string.IsNullOrEmpty(issueIdsValue))
        {
            issueIds.AddRange(issueIdsValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        // Try to extract from arguments
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--issue-id=", StringComparison.OrdinalIgnoreCase))
            {
                var issueId = arg[11..];
                if (!string.IsNullOrEmpty(issueId) && !issueIds.Contains(issueId))
                {
                    issueIds.Add(issueId);
                }
            }
        }

        // If no issues specified, discover from issues directory
        if (issueIds.Count == 0)
        {
            var issuesDir = Path.Combine(_workspace.AosRootPath, "spec", "issues");
            if (Directory.Exists(issuesDir))
            {
                var issueFiles = Directory.GetFiles(issuesDir, "ISS-*.json");
                foreach (var issueFile in issueFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(issueFile, ct);
                        using var doc = JsonDocument.Parse(json);
                        
                        // Check if this issue belongs to the parent task
                        if (doc.RootElement.TryGetProperty("taskId", out var taskIdElement) &&
                            taskIdElement.GetString() == parentTaskId)
                        {
                            if (doc.RootElement.TryGetProperty("id", out var idElement))
                            {
                                var issueId = idElement.GetString();
                                if (!string.IsNullOrEmpty(issueId) && !issueIds.Contains(issueId))
                                {
                                    issueIds.Add(issueId);
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Skip invalid issue files
                    }
                }
            }
        }

        return issueIds.AsReadOnly();
    }

    /// <summary>
    /// Extracts context pack ID from the request.
    /// </summary>
    private static string? ExtractContextPackId(CommandRequest request)
    {
        // Try to extract from options
        if (request.Options.TryGetValue("context-pack-id", out var contextPackId) && !string.IsNullOrEmpty(contextPackId))
        {
            return contextPackId;
        }

        // Try to extract from arguments
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--context-pack-id=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[18..];
            }
        }

        return null;
    }
}
