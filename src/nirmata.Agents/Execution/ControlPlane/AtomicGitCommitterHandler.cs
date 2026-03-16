using nirmata.Agents.Execution.Execution.AtomicGitCommitter;
using nirmata.Agents.Execution.Validation;
using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using System.Text.Json;

namespace nirmata.Agents.Execution.ControlPlane;

/// <summary>
/// Command handler for the Atomic Git Committer phase of the orchestrator workflow.
/// Coordinates atomic Git commits with scope enforcement, gating checks, and phase routing.
/// </summary>
public sealed class AtomicGitCommitterHandler
{
    private readonly IAtomicGitCommitter _atomicGitCommitter;
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AtomicGitCommitterHandler"/> class.
    /// </summary>
    public AtomicGitCommitterHandler(
        IAtomicGitCommitter atomicGitCommitter,
        IWorkspace workspace,
        IStateStore stateStore)
    {
        _atomicGitCommitter = atomicGitCommitter ?? throw new ArgumentNullException(nameof(atomicGitCommitter));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    /// <summary>
    /// Handles the atomic git committer phase command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Extract task ID from request inputs
            var taskId = ExtractTaskId(request);
            if (string.IsNullOrEmpty(taskId))
            {
                return CommandRouteResult.Failure(1, "Task ID is required for atomic git commit.");
            }

            // Read the current state to get file scopes and changed files
            var snapshot = _stateStore.ReadSnapshot();
            var currentTaskId = snapshot?.Cursor?.TaskId;

            if (string.IsNullOrEmpty(currentTaskId))
            {
                return CommandRouteResult.Failure(2, "No current task found in state cursor.");
            }

            // Build the task directory path
            var taskDirectory = Path.Combine(
                _workspace.RepositoryRootPath,
                ".aos",
                "spec",
                "tasks",
                currentTaskId);

            if (!Directory.Exists(taskDirectory))
            {
                return CommandRouteResult.Failure(3, $"Task directory not found: {taskDirectory}");
            }

            // Read plan.json to extract file scopes
            var planPath = Path.Combine(taskDirectory, "plan.json");
            if (!File.Exists(planPath))
            {
                return CommandRouteResult.Failure(4, $"Plan file not found: {planPath}");
            }

            var planJson = await File.ReadAllTextAsync(planPath, ct);
            var planValidation = ArtifactContractValidator.ValidateTaskPlan(
                artifactPath: planPath,
                artifactJson: planJson,
                aosRootPath: _workspace.AosRootPath,
                readBoundary: "atomic-git-committer-handler");
            if (!planValidation.IsValid)
            {
                return CommandRouteResult.Failure(
                    6,
                    $"Atomic git commit blocked by contract validation gate. {planValidation.CreateFailureMessage()}");
            }

            var fileScopes = ExtractFileScopes(planJson);
            Console.WriteLine($"[AtomicGitCommitterHandler] File scopes: {string.Join(", ", fileScopes)}");

            // Get changed files from the workspace (git status)
            var changedFiles = await GetChangedFilesAsync(ct);
            Console.WriteLine($"[AtomicGitCommitterHandler] Changed files: {string.Join(", ", changedFiles)}");

            // Gate: Check intersection non-empty before proceeding
            var intersection = StagingIntersection.Compute(changedFiles, fileScopes);
            Console.WriteLine($"[AtomicGitCommitterHandler] Intersection: {string.Join(", ", intersection)}");

            if (intersection.Count == 0)
            {
                // No files to commit - update evidence with null commit
                var emptyDiffStat = new DiffStat { FilesChanged = 0, Insertions = 0, Deletions = 0 };
                TaskEvidenceUpdater.UpdateWithoutCommit(
                    _workspace.AosRootPath,
                    currentTaskId,
                    runId,
                    emptyDiffStat);

                // Route to Verifier (no commit needed, but execution is complete)
                await UpdateCursorExecutionStatusAsync(true, ct);

                return CommandRouteResult.Success(
                    $"Atomic git commit completed for {currentTaskId}. No files to stage (intersection empty). ");
            }

            // Extract summary from request or use default
            var summary = ExtractSummary(request) ?? $"Task execution for {currentTaskId}";

            // Create commit request
            var commitRequest = new CommitRequest
            {
                TaskId = currentTaskId,
                FileScopes = fileScopes,
                ChangedFiles = changedFiles,
                Summary = summary
            };

            // Delegate to IAtomicGitCommitter for commit
            var commitResult = await _atomicGitCommitter.CommitAsync(commitRequest, ct);

            if (commitResult.IsSuccess)
            {
                // Update task evidence with commit metadata
                if (commitResult.DiffStat != null)
                {
                    TaskEvidenceUpdater.UpdateWithCommit(
                        _workspace.AosRootPath,
                        currentTaskId,
                        runId,
                        commitResult.CommitHash,
                        commitResult.DiffStat);
                }

                // Route to Verifier on success
                await UpdateCursorExecutionStatusAsync(true, ct);

                return CommandRouteResult.Success(
                    $"Atomic git commit completed successfully for {currentTaskId}. " +
                    $"Commit hash: {commitResult.CommitHash}. " +
                    $"Files staged: {commitResult.FilesStaged.Count}. " +
                    $"Diff: +{commitResult.DiffStat?.Insertions}/-{commitResult.DiffStat?.Deletions}");
            }
            else
            {
                // Update task evidence without commit (failure case)
                var failureDiffStat = commitResult.DiffStat ?? new DiffStat { FilesChanged = 0, Insertions = 0, Deletions = 0 };
                TaskEvidenceUpdater.UpdateWithoutCommit(
                    _workspace.AosRootPath,
                    currentTaskId,
                    runId,
                    failureDiffStat);

                // Route to FixPlanner on failure
                await UpdateCursorExecutionStatusAsync(false, ct);

                return CommandRouteResult.Failure(
                    5,
                    $"Atomic git commit failed for {currentTaskId}: {commitResult.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Atomic git committer handler failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the cursor execution status in state.json for routing decisions.
    /// Success → Verifier, Failure → FixPlanner
    /// </summary>
    private async Task UpdateCursorExecutionStatusAsync(bool success, CancellationToken ct)
    {
        var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
        if (!File.Exists(statePath))
        {
            Console.WriteLine($"[AtomicGitCommitterHandler] State file not found at {statePath}");
            return;
        }

        var stateJson = await File.ReadAllTextAsync(statePath, ct);
        var state = JsonSerializer.Deserialize<StateSnapshot>(stateJson, JsonOptions);
        if (state?.Cursor == null)
        {
            Console.WriteLine("[AtomicGitCommitterHandler] State or cursor is null");
            return;
        }

        var updatedCursor = new StateCursor
        {
            MilestoneId = state.Cursor.MilestoneId,
            MilestoneStatus = state.Cursor.MilestoneStatus,
            PhaseId = state.Cursor.PhaseId,
            PhaseStatus = state.Cursor.PhaseStatus,
            TaskId = state.Cursor.TaskId,
            TaskStatus = success ? "completed" : "failed",
            StepId = state.Cursor.StepId,
            StepStatus = state.Cursor.StepStatus
        };

        var updatedState = new StateSnapshot
        {
            SchemaVersion = state.SchemaVersion,
            Cursor = updatedCursor
        };

        var updatedStateJson = JsonSerializer.Serialize(updatedState, JsonOptions);
        await File.WriteAllTextAsync(statePath, updatedStateJson, ct);
        Console.WriteLine($"[AtomicGitCommitterHandler] Updated state cursor status to {updatedCursor.TaskStatus}");
    }

    private static string? ExtractTaskId(CommandRequest request)
    {
        // Try to extract from options if present
        if (request.Options.TryGetValue("task-id", out var taskIdValue) && !string.IsNullOrEmpty(taskIdValue))
        {
            return taskIdValue;
        }

        // Try to extract from arguments if present
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--task-id=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[10..];
            }
        }

        // Return null - will use current cursor task
        return null;
    }

    private static string? ExtractSummary(CommandRequest request)
    {
        // Try to extract from options if present
        if (request.Options.TryGetValue("summary", out var summaryValue) && !string.IsNullOrEmpty(summaryValue))
        {
            return summaryValue;
        }

        // Try to extract from arguments if present
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--summary=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[10..];
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ExtractFileScopes(string planJson)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(planJson);

            var scopes = new List<string>();

            if (document.RootElement.TryGetProperty("fileScopes", out var fileScopesElement) &&
                fileScopesElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var scopeElement in fileScopesElement.EnumerateArray())
                {
                    if (scopeElement.ValueKind == System.Text.Json.JsonValueKind.Object &&
                        scopeElement.TryGetProperty("path", out var pathElement) &&
                        pathElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var scopePath = pathElement.GetString();
                        if (!string.IsNullOrWhiteSpace(scopePath))
                        {
                            scopes.Add(scopePath);
                        }
                    }
                }
            }

            return scopes.AsReadOnly();
        }
        catch
        {
            // Return empty list if parsing fails - handler will validate
            return Array.Empty<string>();
        }
    }

    private async Task<IReadOnlyList<string>> GetChangedFilesAsync(CancellationToken ct)
    {
        try
        {
            // Use git status to get changed files (include untracked files individually with -u)
            var gitRunner = new GitCommandRunner(_workspace.RepositoryRootPath);
            Console.WriteLine($"[AtomicGitCommitterHandler] Running git status in {_workspace.RepositoryRootPath}");
            var result = await gitRunner.ExecuteAsync("status --porcelain=v1 -uall", ct);

            if (!result.IsSuccess)
            {
                Console.WriteLine($"[AtomicGitCommitterHandler] git status failed: {result.Stderr}");
                return Array.Empty<string>();
            }

            Console.WriteLine($"[AtomicGitCommitterHandler] git status output: {result.Stdout}");

            var changedFiles = new List<string>();
            var lines = result.Stdout.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                if (line.Length >= 3)
                {
                    // Parse git status --porcelain output
                    // Format: XY filename or XY filename -> newfilename (for renames)
                    var filePath = line[3..].Trim();

                    // Handle rename detection (format: "filename -> newfilename")
                    if (filePath.Contains(" -> "))
                    {
                        var parts = filePath.Split(new[] { " -> " }, StringSplitOptions.None);
                        if (parts.Length == 2)
                        {
                            // Add the destination of the rename
                            filePath = parts[1].Trim();
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        changedFiles.Add(filePath);
                    }
                }
            }

            Console.WriteLine($"[AtomicGitCommitterHandler] Found {changedFiles.Count} changed files");
            return changedFiles.AsReadOnly();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AtomicGitCommitterHandler] GetChangedFilesAsync failed: {ex}");
            return Array.Empty<string>();
        }
    }
}
