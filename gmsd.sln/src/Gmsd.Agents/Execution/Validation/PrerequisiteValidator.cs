using System.Text.Json;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;

namespace Gmsd.Agents.Execution.Validation;

/// <summary>
/// Validates workspace prerequisites before workflow execution.
/// Checks .aos/spec/, .aos/state/ requirements and provides conversational recovery options.
/// </summary>
public sealed class PrerequisiteValidator : IPrerequisiteValidator
{
    private readonly IWorkspace _workspace;
    private readonly IStateStore _stateStore;
    private readonly ILogger<PrerequisiteValidator>? _logger;

    // Expected paths relative to workspace root
    private const string AosDirectory = ".aos";
    private const string SpecDirectory = ".aos/spec";
    private const string StateDirectory = ".aos/state";
    private const string PlansDirectory = ".aos/plans";
    private const string ProjectSpecFile = ".aos/spec/project-spec.json";
    private const string RoadmapFile = ".aos/spec/roadmap.json";
    private const string StateFile = ".aos/state/state.json";

    /// <summary>
    /// Initializes a new instance of the <see cref="PrerequisiteValidator"/> class.
    /// </summary>
    public PrerequisiteValidator(IWorkspace workspace, IStateStore stateStore, ILogger<PrerequisiteValidator>? logger = null)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<PrerequisiteValidationResult> EnsureWorkspaceInitializedAsync(CancellationToken ct = default)
    {
        var attemptedRepairs = new List<string>
        {
            "Ensure .aos/state/events.ndjson exists",
            "Ensure .aos/state/state.json exists with deterministic baseline",
            "Derive deterministic state snapshot from ordered events when snapshot is missing or stale"
        };

        try
        {
            _stateStore.EnsureWorkspaceInitialized();
            return Task.FromResult(PrerequisiteValidationResult.Satisfied("WorkspaceInitialization"));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Workspace state readiness preflight failed.");

            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.State,
                Description = $"Failed to establish deterministic workspace state readiness: {ex.Message}",
                ExpectedPath = ".aos/state/",
                SuggestedCommand = "/init",
                FailureCode = "state-readiness-failure",
                FailingPrerequisite = ".aos/state/state.json",
                AttemptedRepairs = attemptedRepairs,
                SuggestedFixes =
                [
                    "Run /init to re-seed workspace state artifacts.",
                    "Validate .aos/state/events.ndjson contains only valid JSON object lines.",
                    "Re-run the workflow command after state repairs complete."
                ],
                RecoveryAction = "Repair workspace state artifacts and retry the command",
                ConversationalPrompt = "I couldn't repair workspace state readiness automatically. Please run /init, fix any invalid .aos/state/events.ndjson entries, and then retry your command."
            };

            return Task.FromResult(PrerequisiteValidationResult.NotSatisfied("WorkspaceInitialization", missing));
        }
    }

    /// <inheritdoc />
    public async Task<PrerequisiteValidationResult> ValidateAsync(string targetPhase, GatingContext context, CancellationToken ct = default)
    {
        _logger?.LogDebug("Validating prerequisites for phase {TargetPhase}", targetPhase);

        var checkedItems = new List<PrerequisiteStatus>();
        var workspaceRoot = _workspace.RepositoryRootPath;

        // First check if workspace is initialized
        var workspaceStatus = CheckWorkspaceStatus(workspaceRoot);
        checkedItems.Add(workspaceStatus);

        if (!workspaceStatus.Exists)
        {
            return CreateMissingWorkspaceResult(targetPhase, workspaceRoot, checkedItems);
        }

        // Phase-specific prerequisite checks
        return targetPhase.ToLowerInvariant() switch
        {
            "interviewer" => await ValidateInterviewerPrerequisitesAsync(targetPhase, workspaceRoot, checkedItems, ct),
            "roadmapper" => await ValidateRoadmapperPrerequisitesAsync(targetPhase, context, workspaceRoot, checkedItems, ct),
            "planner" => await ValidatePlannerPrerequisitesAsync(targetPhase, context, workspaceRoot, checkedItems, ct),
            "executor" => await ValidateExecutorPrerequisitesAsync(targetPhase, context, workspaceRoot, checkedItems, ct),
            "verifier" => await ValidateVerifierPrerequisitesAsync(targetPhase, workspaceRoot, checkedItems, ct),
            "fixplanner" => await ValidateFixPlannerPrerequisitesAsync(targetPhase, context, workspaceRoot, checkedItems, ct),
            _ => PrerequisiteValidationResult.Satisfied(targetPhase, checkedItems)
        };
    }

    /// <inheritdoc />
    public Task<WorkspaceBootstrapResult> CheckWorkspaceBootstrapAsync(CancellationToken ct = default)
    {
        var workspaceRoot = _workspace.RepositoryRootPath;
        var aosPath = Path.Combine(workspaceRoot, AosDirectory);
        var specPath = Path.Combine(workspaceRoot, SpecDirectory);
        var statePath = Path.Combine(workspaceRoot, StateDirectory);

        var hasAos = Directory.Exists(aosPath);
        var hasSpec = Directory.Exists(specPath);
        var hasState = Directory.Exists(statePath);

        // Look for spec files
        var foundSpecFiles = new List<string>();
        if (hasSpec)
        {
            try
            {
                var specDir = new DirectoryInfo(specPath);
                foundSpecFiles = specDir.GetFiles("*.json")
                    .Select(f => f.Name)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to scan spec directory");
            }
        }

        var isInitialized = hasAos && hasSpec && hasState &&
                           foundSpecFiles.Any(f => f.Contains("spec", StringComparison.OrdinalIgnoreCase));

        var result = new WorkspaceBootstrapResult
        {
            IsInitialized = isInitialized,
            HasAosDirectory = hasAos,
            HasSpecDirectory = hasSpec,
            HasStateDirectory = hasState,
            FoundSpecFiles = foundSpecFiles,
            BootstrapCommand = "/init",
            BootstrapPrompt = "Welcome! I need to set up a workspace first. Should I initialize the workspace in this directory?"
        };

        return Task.FromResult(result);
    }

    private PrerequisiteStatus CheckWorkspaceStatus(string workspaceRoot)
    {
        var aosPath = Path.Combine(workspaceRoot, AosDirectory);
        var exists = Directory.Exists(aosPath);

        return new PrerequisiteStatus
        {
            Type = PrerequisiteType.Workspace,
            Exists = exists,
            Path = aosPath
        };
    }

    private Task<PrerequisiteValidationResult> ValidateInterviewerPrerequisitesAsync(
        string targetPhase, string workspaceRoot, List<PrerequisiteStatus> checkedItems, CancellationToken ct)
    {
        // Interviewer can run with no prerequisites - it's the starting point
        return Task.FromResult(PrerequisiteValidationResult.Satisfied(targetPhase, checkedItems));
    }

    private async Task<PrerequisiteValidationResult> ValidateRoadmapperPrerequisitesAsync(
        string targetPhase, GatingContext context, string workspaceRoot, List<PrerequisiteStatus> checkedItems, CancellationToken ct)
    {
        // Roadmapper requires project spec
        var specPath = Path.Combine(workspaceRoot, ProjectSpecFile);
        var specExists = await FileExistsAndValidAsync(specPath, ct);

        checkedItems.Add(new PrerequisiteStatus
        {
            Type = PrerequisiteType.ProjectSpec,
            Exists = specExists,
            Path = specPath
        });

        if (!specExists)
        {
            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.ProjectSpec,
                Description = "Project specification is required before creating a roadmap",
                ExpectedPath = ProjectSpecFile,
                SuggestedCommand = "/interview",
                RecoveryAction = "Start the project interviewer to create a project specification",
                ConversationalPrompt = "I need a project specification before creating a roadmap. Would you like me to start the project interviewer?"
            };

            return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
        }

        return PrerequisiteValidationResult.Satisfied(targetPhase, checkedItems);
    }

    private async Task<PrerequisiteValidationResult> ValidatePlannerPrerequisitesAsync(
        string targetPhase, GatingContext context, string workspaceRoot, List<PrerequisiteStatus> checkedItems, CancellationToken ct)
    {
        // Planner requires project spec
        var specPath = Path.Combine(workspaceRoot, ProjectSpecFile);
        var specExists = await FileExistsAndValidAsync(specPath, ct);

        checkedItems.Add(new PrerequisiteStatus
        {
            Type = PrerequisiteType.ProjectSpec,
            Exists = specExists,
            Path = specPath
        });

        if (!specExists)
        {
            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.ProjectSpec,
                Description = "Project specification is required before planning phases",
                ExpectedPath = ProjectSpecFile,
                SuggestedCommand = "/interview",
                RecoveryAction = "Start the project interviewer to create a project specification",
                ConversationalPrompt = "I need a project specification first. Would you like me to start the project interviewer?"
            };

            return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
        }

        // Planner requires roadmap
        var roadmapPath = Path.Combine(workspaceRoot, RoadmapFile);
        var roadmapExists = await FileExistsAndValidAsync(roadmapPath, ct);

        checkedItems.Add(new PrerequisiteStatus
        {
            Type = PrerequisiteType.Roadmap,
            Exists = roadmapExists,
            Path = roadmapPath
        });

        if (!roadmapExists)
        {
            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.Roadmap,
                Description = "Roadmap is required before planning phases",
                ExpectedPath = RoadmapFile,
                SuggestedCommand = "/roadmap --create",
                RecoveryAction = "Create a roadmap based on the project specification",
                ConversationalPrompt = "I need to create a roadmap before planning phases. Should I create the roadmap based on the project spec?"
            };

            return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
        }

        return PrerequisiteValidationResult.Satisfied(targetPhase, checkedItems);
    }

    private async Task<PrerequisiteValidationResult> ValidateExecutorPrerequisitesAsync(
        string targetPhase, GatingContext context, string workspaceRoot, List<PrerequisiteStatus> checkedItems, CancellationToken ct)
    {
        // Executor requires project spec, roadmap, and a plan for current cursor
        var specPath = Path.Combine(workspaceRoot, ProjectSpecFile);
        var specExists = await FileExistsAndValidAsync(specPath, ct);

        checkedItems.Add(new PrerequisiteStatus
        {
            Type = PrerequisiteType.ProjectSpec,
            Exists = specExists,
            Path = specPath
        });

        if (!specExists)
        {
            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.ProjectSpec,
                Description = "Project specification is required before executing tasks",
                ExpectedPath = ProjectSpecFile,
                SuggestedCommand = "/interview",
                RecoveryAction = "Start the project interviewer to create a project specification",
                ConversationalPrompt = "I need a project specification first. Would you like me to start the project interviewer?"
            };

            return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
        }

        var roadmapPath = Path.Combine(workspaceRoot, RoadmapFile);
        var roadmapExists = await FileExistsAndValidAsync(roadmapPath, ct);

        checkedItems.Add(new PrerequisiteStatus
        {
            Type = PrerequisiteType.Roadmap,
            Exists = roadmapExists,
            Path = roadmapPath
        });

        if (!roadmapExists)
        {
            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.Roadmap,
                Description = "Roadmap is required before executing tasks",
                ExpectedPath = RoadmapFile,
                SuggestedCommand = "/roadmap --create",
                RecoveryAction = "Create a roadmap based on the project specification",
                ConversationalPrompt = "I need to create a roadmap first. Should I create the roadmap based on the project spec?"
            };

            return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
        }

        // Check for plan at current cursor position
        var cursor = context.CurrentCursor ?? "unknown";
        var planPath = Path.Combine(workspaceRoot, PlansDirectory, $"{cursor}.json");
        var planExists = await FileExistsAndValidAsync(planPath, ct);

        // Also check for any plan file if cursor is unknown
        if (!planExists && cursor == "unknown")
        {
            var plansDir = Path.Combine(workspaceRoot, PlansDirectory);
            if (Directory.Exists(plansDir))
            {
                var planFiles = Directory.GetFiles(plansDir, "*.json");
                planExists = planFiles.Length > 0;
            }
        }

        checkedItems.Add(new PrerequisiteStatus
        {
            Type = PrerequisiteType.Plan,
            Exists = planExists,
            Path = planPath
        });

        if (!planExists)
        {
            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.Plan,
                Description = $"No plan exists for phase {cursor}",
                ExpectedPath = $".aos/plans/{cursor}.json",
                SuggestedCommand = $"/plan --phase {cursor}",
                RecoveryAction = $"Create an execution plan for phase {cursor}",
                ConversationalPrompt = $"There's no plan for phase {cursor} yet. Would you like me to create a plan for this phase?"
            };

            return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
        }

        return PrerequisiteValidationResult.Satisfied(targetPhase, checkedItems);
    }

    private async Task<PrerequisiteValidationResult> ValidateVerifierPrerequisitesAsync(
        string targetPhase, string workspaceRoot, List<PrerequisiteStatus> checkedItems, CancellationToken ct)
    {
        // Verifier requires state to know what to verify
        var statePath = Path.Combine(workspaceRoot, StateFile);
        var stateExists = await FileExistsAndValidAsync(statePath, ct);

        checkedItems.Add(new PrerequisiteStatus
        {
            Type = PrerequisiteType.State,
            Exists = stateExists,
            Path = statePath
        });

        if (!stateExists)
        {
            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.State,
                Description = "State file is required for verification operations",
                ExpectedPath = StateFile,
                SuggestedCommand = "/init",
                RecoveryAction = "Initialize the workspace with fresh state",
                ConversationalPrompt = "I need workspace state to proceed with verification. Should I initialize the workspace?"
            };

            return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
        }

        return PrerequisiteValidationResult.Satisfied(targetPhase, checkedItems);
    }

    private async Task<PrerequisiteValidationResult> ValidateFixPlannerPrerequisitesAsync(
        string targetPhase, GatingContext context, string workspaceRoot, List<PrerequisiteStatus> checkedItems, CancellationToken ct)
    {
        // FixPlanner requires project spec at minimum
        var specPath = Path.Combine(workspaceRoot, ProjectSpecFile);
        var specExists = await FileExistsAndValidAsync(specPath, ct);

        checkedItems.Add(new PrerequisiteStatus
        {
            Type = PrerequisiteType.ProjectSpec,
            Exists = specExists,
            Path = specPath
        });

        if (!specExists)
        {
            var missing = new MissingPrerequisiteDetail
            {
                Type = PrerequisiteType.ProjectSpec,
                Description = "Project specification is required for fix planning",
                ExpectedPath = ProjectSpecFile,
                SuggestedCommand = "/interview",
                RecoveryAction = "Start the project interviewer to create a project specification",
                ConversationalPrompt = "I need a project specification for fix planning. Would you like me to start the project interviewer?"
            };

            return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
        }

        return PrerequisiteValidationResult.Satisfied(targetPhase, checkedItems);
    }

    private PrerequisiteValidationResult CreateMissingWorkspaceResult(
        string targetPhase, string workspaceRoot, List<PrerequisiteStatus> checkedItems)
    {
        var aosPath = Path.Combine(workspaceRoot, AosDirectory);

        var missing = new MissingPrerequisiteDetail
        {
            Type = PrerequisiteType.Workspace,
            Description = "Workspace is not initialized",
            ExpectedPath = AosDirectory,
            SuggestedCommand = "/init",
            RecoveryAction = "Initialize the workspace in this directory",
            ConversationalPrompt = "Welcome! I need to set up a workspace first. Should I initialize the workspace in this directory?"
        };

        return PrerequisiteValidationResult.NotSatisfied(targetPhase, missing, checkedItems);
    }

    private async Task<bool> FileExistsAndValidAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            // Validate JSON is well-formed
            var content = await File.ReadAllTextAsync(path, ct);
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger?.LogWarning("Prerequisite file {Path} is empty", path);
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(content);
                return true;
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "Prerequisite file {Path} contains invalid JSON", path);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to check prerequisite file {Path}", path);
            return false;
        }
    }
}
