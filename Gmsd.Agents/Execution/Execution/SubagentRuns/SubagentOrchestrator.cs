using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;

namespace Gmsd.Agents.Execution.Execution.SubagentRuns;

/// <summary>
/// Orchestrates subagent runs with fresh context isolation and per-invocation RUN record creation.
/// </summary>
public sealed class SubagentOrchestrator : ISubagentOrchestrator
{
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly IWorkspace _workspace;
    private readonly ILogger<SubagentOrchestrator> _logger;

    public SubagentOrchestrator(
        IRunLifecycleManager runLifecycleManager,
        IWorkspace workspace,
        ILogger<SubagentOrchestrator> logger)
    {
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SubagentRunResult> RunSubagentAsync(SubagentRunRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var runId = string.Empty;
        var modifiedFiles = new List<string>();
        var toolCalls = new List<SubagentToolCall>();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Create a distinct RUN record for this subagent invocation (Task 3.3)
            var runContext = await _runLifecycleManager.StartRunAsync(ct);
            runId = runContext.RunId;

            // Record subagent start command (Task 3.4 - evidence capture)
            await _runLifecycleManager.RecordCommandAsync(runId, "subagent", "start", "running", ct);

            // Load context packs with budget enforcement (Task 3.2)
            var contextPackResult = await LoadContextPacksAsync(request, runId, ct);
            if (!contextPackResult.Success)
            {
                throw new SubagentContextLoadException(
                    $"Failed to load context packs: {contextPackResult.ErrorMessage}",
                    contextPackResult.FailedPackId);
            }

            // Initialize fresh context isolation (Task 3.1)
            var isolatedContext = CreateIsolatedExecutionContext(request, contextPackResult.Packs);
            
            _logger.LogInformation(
                "Initialized fresh context isolation for run {RunId} with {PackCount} context packs",
                runId,
                contextPackResult.Packs.Count);

            // Store subagent-specific metadata in the run
            var runMetadata = new Dictionary<string, object>
            {
                ["taskId"] = request.TaskId,
                ["subagentConfig"] = request.SubagentConfig,
                ["contextPackIds"] = request.ContextPackIds.ToList(),
                ["parentRunId"] = request.ParentRunId ?? string.Empty,
                ["correlationId"] = request.CorrelationId ?? string.Empty,
                ["budget"] = new
                {
                    maxIterations = request.Budget.MaxIterations,
                    maxToolCalls = request.Budget.MaxToolCalls,
                    maxExecutionTimeSeconds = request.Budget.MaxExecutionTimeSeconds,
                    maxTokens = request.Budget.MaxTokens
                }
            };

            // Validate file scope before execution
            foreach (var filePath in request.AllowedFileScope)
            {
                if (!IsPathInAllowedScope(filePath, request.AllowedFileScope))
                {
                    var errorMessage = $"File '{filePath}' is outside allowed scope";
                    _logger.LogError(
                        "Scope violation for subagent run {RunId}, task {TaskId}: {ErrorMessage}",
                        runId,
                        request.TaskId,
                        errorMessage);

                    await _runLifecycleManager.FinishRunAsync(runId, false, new Dictionary<string, object>
                    {
                        ["error"] = errorMessage,
                        ["scopeViolation"] = true,
                        ["targetPath"] = filePath
                    }, ct);

                    return CreateFailureResult(runId, request.TaskId, errorMessage);
                }
            }

            // Execute subagent logic with budget enforcement (Task 3.2)
            var executionResult = await ExecuteSubagentLogicWithBudgetAsync(
                request, 
                isolatedContext, 
                modifiedFiles, 
                toolCalls, 
                ct);

            stopwatch.Stop();

            // Generate normalized output and deterministic hash
            var normalizedOutput = CreateNormalizedOutput(request, modifiedFiles, toolCalls, stopwatch.Elapsed);
            var deterministicHash = ComputeDeterministicHash(normalizedOutput);

            // Prepare result data for run record
            var resultData = new Dictionary<string, object>
            {
                ["taskId"] = request.TaskId,
                ["success"] = executionResult.Success,
                ["normalizedOutput"] = normalizedOutput,
                ["deterministicHash"] = deterministicHash,
                ["modifiedFiles"] = modifiedFiles,
                ["toolCallCount"] = toolCalls.Count,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds,
                ["metrics"] = new
                {
                    iterationCount = executionResult.Iterations,
                    toolCallCount = toolCalls.Count,
                    executionTimeMs = stopwatch.ElapsedMilliseconds,
                    tokensConsumed = executionResult.TokensConsumed
                }
            };

            if (!executionResult.Success && executionResult.ErrorMessage != null)
            {
                resultData["error"] = executionResult.ErrorMessage;
            }

            // Record subagent execution completion command (Task 3.4 - evidence capture)
            await _runLifecycleManager.RecordCommandAsync(
                runId,
                "subagent",
                executionResult.Success ? "complete" : "fail",
                executionResult.Success ? "success" : "failed",
                ct);

            // Write tool calls to evidence folder (Task 3.4 - tool call evidence)
            await WriteToolCallsEvidenceAsync(runId, toolCalls, ct);

            // Close the RUN record with final status
            await _runLifecycleManager.FinishRunAsync(runId, executionResult.Success, resultData, ct);

            _logger.LogInformation(
                "Completed subagent run {RunId} for task {TaskId} in {ElapsedMs}ms",
                runId,
                request.TaskId,
                stopwatch.ElapsedMilliseconds);

            return new SubagentRunResult
            {
                Success = executionResult.Success,
                RunId = runId,
                TaskId = request.TaskId,
                NormalizedOutput = normalizedOutput,
                DeterministicHash = deterministicHash,
                ErrorMessage = executionResult.ErrorMessage,
                ModifiedFiles = modifiedFiles.AsReadOnly(),
                ToolCalls = toolCalls.AsReadOnly(),
                EvidenceArtifacts = new[] { $".aos/evidence/runs/{runId}/" },
                Metrics = new SubagentExecutionMetrics
                {
                    IterationCount = executionResult.Iterations,
                    ToolCallCount = toolCalls.Count,
                    ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                    TokensConsumed = executionResult.TokensConsumed
                },
                ResultData = resultData
            };
        }
        catch (SubagentBudgetExceededException ex)
        {
            stopwatch.Stop();
            return await HandleFailureAsync(runId, request.TaskId, ex, "budget_exceeded", stopwatch.ElapsedMilliseconds, ct);
        }
        catch (SubagentScopeViolationException ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = "scope_violation",
                ["targetPath"] = ex.TargetPath,
                ["allowedScopes"] = ex.AllowedScopes,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "scope_violation", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
        catch (SubagentTimeoutException ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = "timeout",
                ["timeoutSeconds"] = ex.TimeoutSeconds,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "timeout", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
        catch (SubagentContextLoadException ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = "context_load_failed",
                ["contextPackId"] = ex.ContextPackId ?? string.Empty,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "context_load_failed", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = "Subagent execution was cancelled",
                ["errorCategory"] = "cancelled",
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "cancelled", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = "unexpected",
                ["exceptionType"] = ex.GetType().Name,
                ["executionTimeMs"] = stopwatch.ElapsedMilliseconds,
                ["stackTrace"] = ex.StackTrace ?? string.Empty
            };
            return await HandleFailureAsync(runId, request.TaskId, ex, "unexpected", stopwatch.ElapsedMilliseconds, ct, errorData);
        }
    }

    private async Task<SubagentExecutionResult> ExecuteSubagentLogicAsync(
        SubagentRunRequest request,
        List<string> modifiedFiles,
        List<SubagentToolCall> toolCalls,
        CancellationToken ct)
    {
        // Placeholder implementation - actual LLM-based subagent execution would integrate here
        // This demonstrates the structure for RUN record creation per invocation

        await Task.Yield();

        // Simulate successful execution
        return new SubagentExecutionResult
        {
            Success = true,
            Iterations = 1,
            TokensConsumed = 0,
            ErrorMessage = null
        };
    }

    private static bool IsPathInAllowedScope(string path, IReadOnlyList<string> allowedScopes)
    {
        var normalizedPath = NormalizePath(path);

        foreach (var scope in allowedScopes)
        {
            var normalizedScope = NormalizePath(scope);

            if (normalizedPath == normalizedScope)
            {
                return true;
            }

            if (normalizedScope.EndsWith("/") || normalizedScope.EndsWith("\\"))
            {
                if (normalizedPath.StartsWith(normalizedScope, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            else
            {
                var scopeWithSeparator = normalizedScope + "/";
                if (normalizedPath.StartsWith(scopeWithSeparator, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                var scopeWithBackslash = normalizedScope + "\\";
                if (normalizedPath.StartsWith(scopeWithBackslash, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    private static string CreateNormalizedOutput(SubagentRunRequest request, List<string> modifiedFiles, List<SubagentToolCall> toolCalls, TimeSpan elapsed)
    {
        var output = new
        {
            action = "subagent_execution",
            subagentConfig = request.SubagentConfig,
            files = modifiedFiles,
            toolCalls = toolCalls.Select(t => new
            {
                toolName = t.ToolName,
                timestamp = t.Timestamp
            }).ToList(),
            summary = new
            {
                taskId = request.TaskId,
                filesModified = modifiedFiles.Count,
                toolCallCount = toolCalls.Count,
                executionTimeMs = (long)elapsed.TotalMilliseconds
            }
        };

        return JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string ComputeDeterministicHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static SubagentRunResult CreateFailureResult(string? runId, string taskId, string errorMessage)
    {
        return new SubagentRunResult
        {
            Success = false,
            RunId = runId ?? string.Empty,
            TaskId = taskId,
            ErrorMessage = errorMessage,
            ModifiedFiles = Array.Empty<string>(),
            ToolCalls = Array.Empty<SubagentToolCall>(),
            EvidenceArtifacts = Array.Empty<string>()
        };
    }

    /// <summary>
    /// Writes tool call evidence to the run's evidence folder (Task 3.4).
    /// </summary>
    private async Task WriteToolCallsEvidenceAsync(string runId, List<SubagentToolCall> toolCalls, CancellationToken ct)
    {
        if (toolCalls.Count == 0)
        {
            return;
        }

        try
        {
            var evidenceFolder = Path.Combine(_workspace.AosRootPath, "evidence", "runs", runId);
            var toolCallsPath = Path.Combine(evidenceFolder, "tool-calls.json");

            var toolCallsData = new
            {
                schemaVersion = 1,
                runId,
                recordedAt = DateTimeOffset.UtcNow,
                count = toolCalls.Count,
                calls = toolCalls.Select(t => new
                {
                    toolName = t.ToolName,
                    arguments = t.Arguments,
                    result = t.Result,
                    timestamp = t.Timestamp
                }).ToList()
            };

            var json = JsonSerializer.Serialize(toolCallsData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await File.WriteAllTextAsync(toolCallsPath, json, ct);

            _logger.LogDebug(
                "Wrote {Count} tool calls to evidence folder for run {RunId}",
                toolCalls.Count,
                runId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to write tool calls evidence for run {RunId}",
                runId);
            // Don't fail the entire execution if evidence writing fails
        }
    }

    /// <summary>
    /// Handles failure by recording error details, closing the run, and returning a failure result.
    /// </summary>
    private async Task<SubagentRunResult> HandleFailureAsync(
        string runId,
        string taskId,
        Exception ex,
        string errorCategory,
        long executionTimeMs,
        CancellationToken ct,
        Dictionary<string, object>? additionalData = null)
    {
        _logger.LogError(
            ex,
            "Subagent run {RunId} failed for task {TaskId} with category {ErrorCategory}: {ErrorMessage}",
            runId,
            taskId,
            errorCategory,
            ex.Message);

        if (!string.IsNullOrEmpty(runId))
        {
            // Record failure command (Task 3.4 - evidence capture)
            await _runLifecycleManager.RecordCommandAsync(runId, "subagent", "fail", "failed", ct);

            // Build error data for run record
            var errorData = new Dictionary<string, object>
            {
                ["error"] = ex.Message,
                ["errorCategory"] = errorCategory,
                ["executionTimeMs"] = executionTimeMs
            };

            // Merge additional data if provided
            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    if (!errorData.ContainsKey(kvp.Key))
                    {
                        errorData[kvp.Key] = kvp.Value;
                    }
                }
            }

            // Close the run record with failure status
            await _runLifecycleManager.FinishRunAsync(runId, false, errorData, ct);
        }

        // Create failure result with error category for upstream handling
        var failureResult = CreateFailureResult(runId, taskId, ex.Message);
        failureResult.ResultData["errorCategory"] = errorCategory;
        failureResult.ResultData["executionTimeMs"] = executionTimeMs;

        return failureResult;
    }

    private sealed class SubagentExecutionResult
    {
        public bool Success { get; init; }
        public int Iterations { get; init; }
        public int TokensConsumed { get; init; }
        public string? ErrorMessage { get; init; }
    }

    /// <summary>
    /// Loads context packs with budget enforcement (Task 3.2).
    /// </summary>
    private async Task<ContextPackLoadResult> LoadContextPacksAsync(SubagentRunRequest request, string runId, CancellationToken ct)
    {
        var packs = new List<ContextPack>();
        var totalBytes = 0;
        var maxTotalBytes = request.Budget.MaxTokens * 4; // Rough approximation: ~4 bytes per token

        foreach (var packId in request.ContextPackIds)
        {
            try
            {
                // Attempt to load context pack from workspace
                var packPath = Path.Combine(_workspace.AosRootPath, "context", "packs", $"{packId}.json");
                
                if (!File.Exists(packPath))
                {
                    _logger.LogWarning("Context pack {PackId} not found at {PackPath}", packId, packPath);
                    return new ContextPackLoadResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"Context pack '{packId}' not found",
                        FailedPackId = packId
                    };
                }

                var packJson = await File.ReadAllTextAsync(packPath, ct);
                var packBytes = Encoding.UTF8.GetByteCount(packJson);

                // Check budget before adding pack
                if (totalBytes + packBytes > maxTotalBytes)
                {
                    _logger.LogError(
                        "Context pack {PackId} ({PackBytes} bytes) exceeds budget. Total would be {TotalBytes}/{MaxBytes}",
                        packId,
                        packBytes,
                        totalBytes + packBytes,
                        maxTotalBytes);

                    throw new SubagentBudgetExceededException(
                        $"Context pack '{packId}' would exceed token budget. " +
                        $"Pack size: {packBytes} bytes, Budget: {maxTotalBytes} bytes.");
                }

                var pack = JsonSerializer.Deserialize<ContextPack>(packJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (pack == null)
                {
                    return new ContextPackLoadResult 
                    { 
                        Success = false, 
                        ErrorMessage = $"Failed to parse context pack '{packId}'",
                        FailedPackId = packId
                    };
                }

                packs.Add(pack);
                totalBytes += packBytes;

                _logger.LogDebug(
                    "Loaded context pack {PackId} ({PackBytes} bytes) for run {RunId}. Total: {TotalBytes}/{MaxBytes}",
                    packId,
                    packBytes,
                    runId,
                    totalBytes,
                    maxTotalBytes);
            }
            catch (SubagentBudgetExceededException)
            {
                throw; // Re-throw budget exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load context pack {PackId}", packId);
                return new ContextPackLoadResult 
                { 
                    Success = false, 
                    ErrorMessage = $"Error loading context pack '{packId}': {ex.Message}",
                    FailedPackId = packId
                };
            }
        }

        // Log budget utilization
        var utilizationPercent = maxTotalBytes > 0 ? (totalBytes * 100.0 / maxTotalBytes) : 0;
        _logger.LogInformation(
            "Loaded {PackCount} context packs for run {RunId}. Total: {TotalBytes} bytes ({Utilization:F1}% of budget)",
            packs.Count,
            runId,
            totalBytes,
            utilizationPercent);

        return new ContextPackLoadResult 
        { 
            Success = true, 
            Packs = packs,
            TotalBytes = totalBytes
        };
    }

    /// <summary>
    /// Creates an isolated execution context for the subagent (Task 3.1).
    /// </summary>
    private IsolatedExecutionContext CreateIsolatedExecutionContext(SubagentRunRequest request, List<ContextPack> packs)
    {
        // Create fresh isolated context - no shared state from parent or previous runs
        var isolatedContext = new IsolatedExecutionContext
        {
            TaskId = request.TaskId,
            SubagentConfig = request.SubagentConfig,
            AllowedFileScope = request.AllowedFileScope.ToList(),
            ContextPacks = packs,
            EnvironmentVariables = new Dictionary<string, string>(), // Fresh env - no inheritance
            WorkingDirectory = Path.Combine(_workspace.AosRootPath, "temp", $"subagent-{Guid.NewGuid():N}"),
            StartTime = DateTimeOffset.UtcNow
        };

        // Create isolated working directory
        try
        {
            Directory.CreateDirectory(isolatedContext.WorkingDirectory);
            
            // Copy context pack files to isolated directory
            foreach (var pack in packs)
            {
                if (pack.Files != null)
                {
                    foreach (var file in pack.Files)
                    {
                        var targetPath = Path.Combine(isolatedContext.WorkingDirectory, file.RelativePath);
                        var targetDir = Path.GetDirectoryName(targetPath);
                        if (!string.IsNullOrEmpty(targetDir))
                        {
                            Directory.CreateDirectory(targetDir);
                        }
                        File.WriteAllText(targetPath, file.Content);
                    }
                }
            }

            _logger.LogDebug(
                "Created isolated execution context at {WorkingDirectory} for task {TaskId}",
                isolatedContext.WorkingDirectory,
                request.TaskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create isolated execution context");
            throw new SubagentContextLoadException(
                $"Failed to initialize isolated context: {ex.Message}");
        }

        return isolatedContext;
    }

    /// <summary>
    /// Executes subagent logic with comprehensive budget enforcement (Task 3.2).
    /// </summary>
    private async Task<SubagentExecutionResult> ExecuteSubagentLogicWithBudgetAsync(
        SubagentRunRequest request,
        IsolatedExecutionContext context,
        List<string> modifiedFiles,
        List<SubagentToolCall> toolCalls,
        CancellationToken ct)
    {
        var iterationCount = 0;
        var tokensConsumed = 0;
        var startTime = DateTimeOffset.UtcNow;
        var maxExecutionTime = TimeSpan.FromSeconds(request.Budget.MaxExecutionTimeSeconds);

        // Create linked cancellation token with timeout
        using var timeoutCts = new CancellationTokenSource(maxExecutionTime);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var linkedCt = linkedCts.Token;

        try
        {
            // Main execution loop with iteration budget
            while (iterationCount < request.Budget.MaxIterations)
            {
                linkedCt.ThrowIfCancellationRequested();

                // Check execution time budget
                var elapsed = DateTimeOffset.UtcNow - startTime;
                if (elapsed > maxExecutionTime)
                {
                    throw new SubagentTimeoutException(
                        $"Subagent execution exceeded time budget of {request.Budget.MaxExecutionTimeSeconds} seconds",
                        request.Budget.MaxExecutionTimeSeconds);
                }

                iterationCount++;

                // Simulate execution step (placeholder for actual LLM-based execution)
                // In real implementation, this would:
                // 1. Call LLM with context
                // 2. Track token usage
                // 3. Execute any tool calls
                // 4. Check tool call budget
                // 5. Update modified files

                await Task.Yield();

                // Check token budget
                tokensConsumed += 100; // Placeholder token count
                if (tokensConsumed > request.Budget.MaxTokens)
                {
                    throw new SubagentBudgetExceededException(
                        $"Subagent execution exceeded token budget of {request.Budget.MaxTokens} tokens");
                }

                // Check tool call budget
                if (toolCalls.Count >= request.Budget.MaxToolCalls)
                {
                    throw new SubagentBudgetExceededException(
                        $"Subagent execution exceeded tool call budget of {request.Budget.MaxToolCalls} calls");
                }

                // For placeholder, complete after one iteration
                break;
            }

            if (iterationCount >= request.Budget.MaxIterations)
            {
                throw new SubagentBudgetExceededException(
                    $"Subagent execution exceeded iteration budget of {request.Budget.MaxIterations} iterations");
            }

            return new SubagentExecutionResult
            {
                Success = true,
                Iterations = iterationCount,
                TokensConsumed = tokensConsumed,
                ErrorMessage = null
            };
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            throw new SubagentTimeoutException(
                $"Subagent execution timed out after {request.Budget.MaxExecutionTimeSeconds} seconds",
                request.Budget.MaxExecutionTimeSeconds);
        }
    }

    /// <summary>
    /// Result of context pack loading operation.
    /// </summary>
    private sealed class ContextPackLoadResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public string? FailedPackId { get; init; }
        public List<ContextPack> Packs { get; init; } = new();
        public int TotalBytes { get; init; }
    }

    /// <summary>
    /// Represents a loaded context pack.
    /// </summary>
    private sealed class ContextPack
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public List<ContextPackFile> Files { get; init; } = new();
        public Dictionary<string, object> Metadata { get; init; } = new();
    }

    /// <summary>
    /// Represents a file within a context pack.
    /// </summary>
    private sealed class ContextPackFile
    {
        public string RelativePath { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
    }

    /// <summary>
    /// Represents an isolated execution context for a subagent run.
    /// </summary>
    private sealed class IsolatedExecutionContext
    {
        public string TaskId { get; init; } = string.Empty;
        public string SubagentConfig { get; init; } = string.Empty;
        public List<string> AllowedFileScope { get; init; } = new();
        public List<ContextPack> ContextPacks { get; init; } = new();
        public Dictionary<string, string> EnvironmentVariables { get; init; } = new();
        public string WorkingDirectory { get; init; } = string.Empty;
        public DateTimeOffset StartTime { get; init; }
    }
}
