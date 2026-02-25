using Gmsd.Agents.Execution.Verification.UatVerifier;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;
using System.Text.Json;

namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Command handler for the Verifier phase of the orchestrator workflow.
/// Coordinates UAT verification with check execution and issue creation.
/// </summary>
public sealed class VerifierHandler
{
    private readonly IUatVerifier _uatVerifier;
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private readonly IRunLifecycleManager _runLifecycleManager;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="VerifierHandler"/> class.
    /// </summary>
    public VerifierHandler(
        IUatVerifier uatVerifier,
        IWorkspace workspace,
        IStateStore stateStore,
        IRunLifecycleManager runLifecycleManager)
    {
        _uatVerifier = uatVerifier ?? throw new ArgumentNullException(nameof(uatVerifier));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
    }

    /// <summary>
    /// Handles the verifier phase command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result with routing information.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Read current state to get task context
            var snapshot = _stateStore.ReadSnapshot();
            var currentTaskId = snapshot?.Cursor?.TaskId;

            if (string.IsNullOrEmpty(currentTaskId))
            {
                return CommandRouteResult.Failure(1, "No current task found in state cursor.");
            }

            // Read task plan to extract acceptance criteria
            var taskDirectory = Path.Combine(
                _workspace.RepositoryRootPath,
                ".aos",
                "spec",
                "tasks",
                currentTaskId);

            Console.WriteLine($"DEBUG: VerifierHandler - Checking task directory: {taskDirectory}");
            Console.WriteLine($"DEBUG: VerifierHandler - Directory.Exists: {Directory.Exists(taskDirectory)}");

            if (!Directory.Exists(taskDirectory))
            {
                return CommandRouteResult.Failure(2, $"Task directory not found: {taskDirectory}");
            }

            var planPath = Path.Combine(taskDirectory, "plan.json");
            Console.WriteLine($"DEBUG: VerifierHandler - Checking plan path: {planPath}");
            Console.WriteLine($"DEBUG: VerifierHandler - File.Exists: {File.Exists(planPath)}");

            if (!File.Exists(planPath))
            {
                return CommandRouteResult.Failure(3, $"Plan file not found: {planPath}");
            }

            var planJson = await File.ReadAllTextAsync(planPath, ct);
            var planValidation = ArtifactContractValidator.ValidateTaskPlan(
                artifactPath: planPath,
                artifactJson: planJson,
                aosRootPath: _workspace.AosRootPath,
                readBoundary: "verifier-handler");
            if (!planValidation.IsValid)
            {
                return CommandRouteResult.Failure(
                    4,
                    $"Verification blocked by contract validation gate. {planValidation.CreateFailureMessage()}");
            }

            // Extract acceptance criteria from plan
            var (criteria, fileScopes) = ExtractAcceptanceCriteria(planJson);

            if (criteria.Count == 0)
            {
                // No acceptance criteria defined - mark as passed by default
                await UpdateCursorVerificationStatusAsync(true, ct);

                return CommandRouteResult.Success(
                    $"No acceptance criteria defined for {currentTaskId}. Verification skipped.");
            }

            // Create verification request
            var verificationRequest = new UatVerificationRequest
            {
                TaskId = currentTaskId,
                RunId = runId,
                AcceptanceCriteria = criteria,
                FileScopes = fileScopes
            };

            // Execute verification
            var verificationResult = await _uatVerifier.VerifyAsync(verificationRequest, ct);

            // Update cursor state with verification status
            await UpdateCursorVerificationStatusAsync(verificationResult.IsPassed, ct);

            // Record command completion
            await _runLifecycleManager.RecordCommandAsync(
                runId,
                "run",
                "verify",
                verificationResult.IsPassed ? "completed" : "failed",
                ct);

            if (verificationResult.IsPassed)
            {
                // Success - return with indication to continue to next phase
                return new CommandRouteResult
                {
                    IsSuccess = true,
                    Output = $"UAT verification passed for {currentTaskId}. " +
                             $"{verificationResult.Checks.Count} check(s) passed. " +
                             $"Result artifact: {verificationResult.RunId}/artifacts/uat-results.json",
                    RoutingHint = "continue" // Signal to orchestrator to proceed
                };
            }
            else
            {
                // Failure - return with routing to FixPlanner
                var failedChecks = verificationResult.Checks.Count(c => !c.Passed && c.IsRequired);
                var issuesCreated = verificationResult.IssuesCreated.Count;

                return new CommandRouteResult
                {
                    IsSuccess = false,
                    ErrorOutput = $"UAT verification failed for {currentTaskId}. " +
                                 $"{failedChecks} required check(s) failed. " +
                                 $"{issuesCreated} issue(s) created.",
                    RoutingHint = "FixPlanner" // Signal to orchestrator to route to FixPlanner
                };
            }
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Verifier handler failed: {ex.Message} StackTrace: {ex.StackTrace}");
        }
    }

    private async Task UpdateCursorVerificationStatusAsync(bool passed, CancellationToken ct)
    {
        try
        {
            var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
            if (!File.Exists(statePath))
            {
                return;
            }

            var stateJson = await File.ReadAllTextAsync(statePath, ct);
            var state = JsonSerializer.Deserialize<StateSnapshot>(stateJson, JsonOptions);
            if (state?.Cursor == null)
            {
                return;
            }

            var updatedCursor = new StateCursor
            {
                MilestoneId = state.Cursor.MilestoneId,
                MilestoneStatus = state.Cursor.MilestoneStatus,
                PhaseId = state.Cursor.PhaseId,
                PhaseStatus = state.Cursor.PhaseStatus,
                TaskId = state.Cursor.TaskId,
                TaskStatus = state.Cursor.TaskStatus,
                StepId = state.Cursor.StepId,
                StepStatus = passed ? "passed" : "failed"
            };

            var updatedState = new StateSnapshot
            {
                SchemaVersion = state.SchemaVersion,
                Cursor = updatedCursor
            };

            var updatedStateJson = JsonSerializer.Serialize(updatedState, JsonOptions);
            await File.WriteAllTextAsync(statePath, updatedStateJson, ct);
        }
        catch
        {
            // Don't fail verification if state update fails
        }
    }

    private static (IReadOnlyList<AcceptanceCriterion> Criteria, IReadOnlyList<FileScope> FileScopes) ExtractAcceptanceCriteria(string planJson)
    {
        var criteria = new List<AcceptanceCriterion>();
        var fileScopes = new List<FileScope>();

        try
        {
            using var document = JsonDocument.Parse(planJson);

            // Extract file scopes
            if (document.RootElement.TryGetProperty("fileScopes", out var fileScopesElement) &&
                fileScopesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var scopeElement in fileScopesElement.EnumerateArray())
                {
                    if (scopeElement.ValueKind == JsonValueKind.Object)
                    {
                        var relativePath = scopeElement.TryGetProperty("path", out var pathProp)
                            ? pathProp.GetString() ?? ""
                            : scopeElement.TryGetProperty("relativePath", out var legacyPathProp)
                                ? legacyPathProp.GetString() ?? ""
                                : "";
                        var scopeType = scopeElement.GetProperty("scopeType").GetString() ?? "";
                        var description = scopeElement.TryGetProperty("description", out var desc)
                            ? desc.GetString()
                            : null;

                        fileScopes.Add(new FileScope
                        {
                            RelativePath = relativePath,
                            ScopeType = scopeType,
                            Description = description
                        });
                    }
                }
            }

            // Extract verification steps as acceptance criteria
            if (document.RootElement.TryGetProperty("verificationSteps", out var verificationSteps) &&
                verificationSteps.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var step in verificationSteps.EnumerateArray())
                {
                    var verificationType = step.GetProperty("verificationType").GetString() ?? "";
                    var description = step.GetProperty("description").GetString() ?? "";
                    var expectedOutcome = step.TryGetProperty("expectedOutcome", out var outcome)
                        ? outcome.GetString()
                        : null;
                    var command = step.TryGetProperty("command", out var cmd)
                        ? cmd.GetString()
                        : null;

                    // Map verification type to check type
                    var checkType = MapVerificationTypeToCheckType(verificationType);

                    criteria.Add(new AcceptanceCriterion
                    {
                        Id = $"criterion-{index + 1:D3}",
                        Description = description,
                        CheckType = checkType,
                        TargetPath = command,
                        ExpectedContent = expectedOutcome,
                        IsRequired = true
                    });

                    index++;
                }
            }

            // If no explicit verification steps, create default criteria from file scopes
            if (criteria.Count == 0 && fileScopes.Count > 0)
            {
                foreach (var scope in fileScopes.Where(s => s.ScopeType is "create" or "modify" or "write"))
                {
                    criteria.Add(new AcceptanceCriterion
                    {
                        Id = $"criterion-file-{criteria.Count + 1:D3}",
                        Description = $"File {scope.RelativePath} should exist",
                        CheckType = UatCheckTypes.FileExists,
                        TargetPath = scope.RelativePath,
                        IsRequired = true
                    });
                }
            }
        }
        catch
        {
            // Return empty lists on parsing failure
        }

        return (criteria.AsReadOnly(), fileScopes.AsReadOnly());
    }

    private static string MapVerificationTypeToCheckType(string verificationType)
    {
        return verificationType.ToLowerInvariant() switch
        {
            "test" => UatCheckTypes.TestPasses,
            "compile" or "build" => UatCheckTypes.BuildSucceeds,
            "lint" or "content" => UatCheckTypes.ContentContains,
            "file" => UatCheckTypes.FileExists,
            _ => UatCheckTypes.FileExists
        };
    }
}
