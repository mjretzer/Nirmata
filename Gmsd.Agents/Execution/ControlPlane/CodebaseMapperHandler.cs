using Gmsd.Agents.Execution.Brownfield.CodebaseScanner;
using Gmsd.Agents.Execution.Brownfield.MapValidator;
using Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public;
using System.Text.Json;
using FileInfo = System.IO.FileInfo;

namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Command handler for the CodebaseMapper workflow.
/// Orchestrates codebase scanning, symbol caching, file graph building, and validation.
/// Supports trigger conditions: new repository, stale map, or explicit request.
/// </summary>
public sealed class CodebaseMapperHandler
{
    private readonly ICodebaseScanner _codebaseScanner;
    private readonly IMapValidator _mapValidator;
    private readonly ISymbolCacheBuilder _symbolCacheBuilder;
    private readonly IWorkspace _workspace;
    private readonly IRunLifecycleManager _runLifecycleManager;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="CodebaseMapperHandler"/> class.
    /// </summary>
    public CodebaseMapperHandler(
        ICodebaseScanner codebaseScanner,
        IMapValidator mapValidator,
        ISymbolCacheBuilder symbolCacheBuilder,
        IWorkspace workspace,
        IRunLifecycleManager runLifecycleManager)
    {
        _codebaseScanner = codebaseScanner ?? throw new ArgumentNullException(nameof(codebaseScanner));
        _mapValidator = mapValidator ?? throw new ArgumentNullException(nameof(mapValidator));
        _symbolCacheBuilder = symbolCacheBuilder ?? throw new ArgumentNullException(nameof(symbolCacheBuilder));
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
    }

    /// <summary>
    /// Handles the codebase mapping command.
    /// Evaluates trigger conditions and executes scan if needed.
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
            // Evaluate trigger conditions to determine if scan is needed
            var triggerCheck = await EvaluateTriggerConditionsAsync(request, ct);

            // If no trigger condition is met and not an explicit request, skip scan
            if (!triggerCheck.ShouldScan && !IsExplicitRequest(request))
            {
                return CommandRouteResult.Success(
                    $"Codebase map is up to date. Skipping scan. Reason: {triggerCheck.Reason}");
            }

            // Record command dispatch
            await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "map", "dispatched", ct);

            // Execute codebase scan
            var scanRequest = new CodebaseScanRequest
            {
                RepositoryPath = _workspace.RepositoryRootPath,
                Options = ExtractScanOptions(request)
            };

            var scanResult = await _codebaseScanner.ScanAsync(scanRequest, null, ct);

            if (!scanResult.IsSuccess)
            {
                await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "map", "failed", ct);
                return CommandRouteResult.Failure(1, $"Codebase scan failed: {scanResult.ErrorMessage}");
            }

            // Validate the generated map
            var validationRequest = new MapValidationRequest
            {
                RepositoryRootPath = _workspace.RepositoryRootPath,
                ValidateSchemaCompliance = true,
                CheckCrossFileInvariants = true
            };

            var validationResult = await _mapValidator.ValidateAsync(validationRequest, ct);

            if (!validationResult.IsValid)
            {
                var errorCount = validationResult.Summary.ErrorCount;
                var warningCount = validationResult.Summary.WarningCount;

                await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "map", "failed", ct);
                return CommandRouteResult.Failure(
                    2,
                    $"Codebase map validation failed with {errorCount} error(s) and {warningCount} warning(s).");
            }

            // Record successful completion
            await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "map", "completed", ct);

            // Build success response
            var solutionCount = scanResult.Solutions.Count;
            var projectCount = scanResult.Projects.Count;
            var validationIssues = validationResult.Summary.WarningCount + validationResult.Summary.InfoCount;

            return CommandRouteResult.Success(
                $"Codebase mapping completed successfully. " +
                $"Scanned {solutionCount} solution(s) and {projectCount} project(s). " +
                $"Validation found {validationIssues} non-critical issue(s). " +
                $"Trigger: {triggerCheck.Reason}");
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Codebase mapper handler failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Evaluates trigger conditions to determine if a codebase scan should be performed.
    /// </summary>
    private async Task<TriggerCheckResult> EvaluateTriggerConditionsAsync(CommandRequest request, CancellationToken ct)
    {
        // Check 1: New repository (no existing codebase map)
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        if (!Directory.Exists(codebasePath))
        {
            return new TriggerCheckResult(true, "New repository - no existing codebase map");
        }

        var mapFilePath = Path.Combine(codebasePath, "map.json");
        if (!File.Exists(mapFilePath))
        {
            return new TriggerCheckResult(true, "New repository - map.json not found");
        }

        // Check 2: Explicit request
        if (IsExplicitRequest(request))
        {
            return new TriggerCheckResult(true, "Explicit request via command options");
        }

        // Check 3: Stale map (older than threshold)
        var staleThreshold = GetStaleThreshold(request);
        if (await IsMapStaleAsync(mapFilePath, staleThreshold, ct))
        {
            return new TriggerCheckResult(true, $"Stale map - older than {staleThreshold.TotalHours:F1} hours");
        }

        // Check 4: Repository changes detected (if we have a previous scan timestamp)
        if (await HaveRepositoryChangesOccurredAsync(mapFilePath, ct))
        {
            return new TriggerCheckResult(true, "Repository changes detected since last scan");
        }

        return new TriggerCheckResult(false, "Map is current - no trigger conditions met");
    }

    /// <summary>
    /// Checks if this is an explicit scan request via command options or arguments.
    /// </summary>
    private static bool IsExplicitRequest(CommandRequest request)
    {
        // Check for explicit flag in options
        if (request.Options.TryGetValue("force", out var forceValue) &&
            (forceValue?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true))
        {
            return true;
        }

        if (request.Options.TryGetValue("refresh", out var refreshValue) &&
            (refreshValue?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true))
        {
            return true;
        }

        // Check for explicit flags in arguments
        foreach (var arg in request.Arguments)
        {
            if (arg.Equals("--force", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--refresh", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-f", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the stale threshold from request options or uses default (24 hours).
    /// </summary>
    private static TimeSpan GetStaleThreshold(CommandRequest request)
    {
        // Default: 24 hours
        var defaultThreshold = TimeSpan.FromHours(24);

        if (request.Options.TryGetValue("staleThreshold", out var thresholdValue) &&
            int.TryParse(thresholdValue?.ToString(), out var hours))
        {
            return TimeSpan.FromHours(hours);
        }

        // Check arguments for --stale-hours
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--stale-hours=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(arg[14..], out var argHours) && argHours > 0)
                {
                    return TimeSpan.FromHours(argHours);
                }
            }
        }

        return defaultThreshold;
    }

    /// <summary>
    /// Checks if the existing map is older than the stale threshold.
    /// </summary>
    private static async Task<bool> IsMapStaleAsync(string mapFilePath, TimeSpan threshold, CancellationToken ct)
    {
        try
        {
            var fileInfo = new FileInfo(mapFilePath);
            if (!fileInfo.Exists)
            {
                return true;
            }

            var lastWriteTime = fileInfo.LastWriteTimeUtc;
            var age = DateTimeOffset.UtcNow - lastWriteTime;

            return age > threshold;
        }
        catch
        {
            // If we can't determine staleness, assume it's stale to be safe
            return true;
        }
    }

    /// <summary>
    /// Checks if repository changes have occurred since the last scan.
    /// Compares git HEAD timestamp or file modification times against scan timestamp.
    /// </summary>
    private async Task<bool> HaveRepositoryChangesOccurredAsync(string mapFilePath, CancellationToken ct)
    {
        try
        {
            // Read the map.json to get the scan timestamp
            var mapJson = await File.ReadAllTextAsync(mapFilePath, ct);
            using var document = JsonDocument.Parse(mapJson);

            DateTimeOffset? scanTimestamp = null;
            if (document.RootElement.TryGetProperty("scanTimestamp", out var timestampElement))
            {
                if (timestampElement.TryGetDateTimeOffset(out var timestamp))
                {
                    scanTimestamp = timestamp;
                }
            }

            if (scanTimestamp == null)
            {
                // Can't determine scan time, assume changes occurred
                return true;
            }

            // Check for .git directory to use git-based detection
            var gitPath = Path.Combine(_workspace.RepositoryRootPath, ".git");
            if (Directory.Exists(gitPath))
            {
                // Check if HEAD has been modified since scan
                var headPath = Path.Combine(gitPath, "HEAD");
                if (File.Exists(headPath))
                {
                    var headInfo = new FileInfo(headPath);
                    if (headInfo.LastWriteTimeUtc > scanTimestamp.Value.UtcDateTime)
                    {
                        return true;
                    }
                }

                // Check refs directory for any updates
                var refsPath = Path.Combine(gitPath, "refs");
                if (Directory.Exists(refsPath))
                {
                    var refFiles = Directory.GetFiles(refsPath, "*", SearchOption.AllDirectories);
                    foreach (var refFile in refFiles)
                    {
                        var refInfo = new FileInfo(refFile);
                        if (refInfo.LastWriteTimeUtc > scanTimestamp.Value.UtcDateTime)
                        {
                            return true;
                        }
                    }
                }
            }

            // Check solution/project files for modifications
            var solutionFiles = Directory.GetFiles(_workspace.RepositoryRootPath, "*.sln", SearchOption.AllDirectories);
            var projectFiles = Directory.GetFiles(_workspace.RepositoryRootPath, "*.csproj", SearchOption.AllDirectories);

            foreach (var file in solutionFiles.Concat(projectFiles).Take(100)) // Limit checks for performance
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc > scanTimestamp.Value.UtcDateTime)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            // If detection fails, assume changes occurred to be safe
            return true;
        }
    }

    /// <summary>
    /// Extracts scan options from the command request.
    /// </summary>
    private static CodebaseScanOptions ExtractScanOptions(CommandRequest request)
    {
        bool? includeHidden = null;
        int? maxDepth = null;
        int? maxFiles = null;
        bool? parallel = null;

        // Check for include-hidden option
        if (request.Options.TryGetValue("includeHidden", out var hiddenValue) &&
            bool.TryParse(hiddenValue?.ToString(), out var includeHiddenParsed))
        {
            includeHidden = includeHiddenParsed;
        }

        // Check for max-depth option
        if (request.Options.TryGetValue("maxDepth", out var depthValue) &&
            int.TryParse(depthValue?.ToString(), out var maxDepthParsed))
        {
            maxDepth = maxDepthParsed;
        }

        // Check for max-files option
        if (request.Options.TryGetValue("maxFiles", out var filesValue) &&
            int.TryParse(filesValue?.ToString(), out var maxFilesParsed))
        {
            maxFiles = maxFilesParsed;
        }

        // Check for parallel option
        if (request.Options.TryGetValue("parallel", out var parallelValue) &&
            bool.TryParse(parallelValue?.ToString(), out var parallelParsed))
        {
            parallel = parallelParsed;
        }

        return new CodebaseScanOptions
        {
            IncludeHiddenDirectories = includeHidden ?? false,
            MaxDepth = maxDepth ?? 0,
            MaxFiles = maxFiles ?? 0,
            EnableParallelProcessing = parallel ?? true
        };
    }

    /// <summary>
    /// Handles the codebase validation command.
    /// Validates existing codebase map without performing a full scan.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result.</returns>
    public async Task<CommandRouteResult> ValidateAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Record command dispatch
            await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "validate", "dispatched", ct);

            // Check if codebase map exists
            var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
            var mapFilePath = Path.Combine(codebasePath, "map.json");

            if (!File.Exists(mapFilePath))
            {
                await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "validate", "failed", ct);
                return CommandRouteResult.Failure(1, "No codebase map found. Run 'openspec map-codebase' first.");
            }

            // Extract validation options from request
            var validationRequest = new MapValidationRequest
            {
                RepositoryRootPath = _workspace.RepositoryRootPath,
                ValidateSchemaCompliance = !request.Options.ContainsKey("skip-schema") && !request.Arguments.Contains("--skip-schema"),
                CheckCrossFileInvariants = !request.Options.ContainsKey("skip-invariants") && !request.Arguments.Contains("--skip-invariants"),
                ValidateDeterminism = request.Options.ContainsKey("determinism") || request.Arguments.Contains("--determinism")
            };

            // Perform validation
            var validationResult = await _mapValidator.ValidateAsync(validationRequest, ct);

            if (!validationResult.IsValid)
            {
                var errorCount = validationResult.Summary.ErrorCount;
                var warningCount = validationResult.Summary.WarningCount;
                var infoCount = validationResult.Summary.InfoCount;

                await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "validate", "failed", ct);
                return CommandRouteResult.Failure(
                    2,
                    $"Codebase validation failed with {errorCount} error(s), {warningCount} warning(s), {infoCount} info message(s).");
            }

            // Record successful completion
            await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "validate", "completed", ct);

            var issues = validationResult.Summary.WarningCount + validationResult.Summary.InfoCount;
            var output = issues == 0
                ? "Codebase validation passed. All artifacts are valid."
                : $"Codebase validation passed with {validationResult.Summary.WarningCount} warning(s) and {validationResult.Summary.InfoCount} info message(s).";

            return CommandRouteResult.Success(output);
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Codebase validation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the refresh-symbols command.
    /// Performs incremental symbol cache update for changed files.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result.</returns>
    public async Task<CommandRouteResult> RefreshSymbolsAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Record command dispatch
            await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "refresh-symbols", "dispatched", ct);

            // Check if codebase map exists
            var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
            var symbolsFilePath = Path.Combine(codebasePath, "cache", "symbols.json");

            // Extract options
            var forceFullRebuild = request.Options.ContainsKey("force") || request.Arguments.Contains("--force");
            var specificFiles = request.Arguments.Where(a => !a.StartsWith("--")).ToList();

            // Build symbol cache request
            var symbolRequest = new SymbolCacheRequest
            {
                RepositoryPath = _workspace.RepositoryRootPath,
                SourceFiles = specificFiles.AsReadOnly(),
                Options = new SymbolCacheOptions
                {
                    IncludePrivateSymbols = !request.Options.ContainsKey("public-only") && !request.Arguments.Contains("--public-only"),
                    IncludeInternalSymbols = !request.Options.ContainsKey("public-only") && !request.Arguments.Contains("--public-only"),
                    IncludeDocumentation = !request.Options.ContainsKey("no-docs") && !request.Arguments.Contains("--no-docs"),
                    EnableParallelProcessing = !request.Options.ContainsKey("no-parallel") && !request.Arguments.Contains("--no-parallel")
                }
            };

            // Perform symbol cache build
            var symbolResult = await _symbolCacheBuilder.BuildAsync(symbolRequest, ct);

            if (!symbolResult.IsSuccess)
            {
                await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "refresh-symbols", "failed", ct);
                return CommandRouteResult.Failure(1, $"Symbol refresh failed: {symbolResult.ErrorMessage}");
            }

            // Record successful completion
            await _runLifecycleManager.RecordCommandAsync(runId, "codebase", "refresh-symbols", "completed", ct);

            var stats = symbolResult.Statistics;
            var mode = forceFullRebuild ? "(full rebuild)" : "(incremental)";
            var output = $"Symbol cache updated {mode}. " +
                $"Extracted {stats.TotalSymbols} symbols from {stats.SourceFileCount} files " +
                $"({stats.TypeCount} types, {stats.MethodCount} methods, {stats.PropertyCount} properties, {stats.FieldCount} fields). " +
                $"Cross-references: {stats.CrossReferenceCount}. Duration: {stats.BuildDuration.TotalSeconds:F2}s";

            return CommandRouteResult.Success(output);
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Symbol refresh failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Result of evaluating trigger conditions.
    /// </summary>
    private sealed record TriggerCheckResult(bool ShouldScan, string Reason);
}
