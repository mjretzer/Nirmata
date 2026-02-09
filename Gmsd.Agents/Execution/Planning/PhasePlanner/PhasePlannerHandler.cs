using Gmsd.Agents.Execution.Planning.PhasePlanner.Assumptions;
using Gmsd.Agents.Execution.Planning.PhasePlanner.ContextGatherer;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public;
using System.Text.Json;

namespace Gmsd.Agents.Execution.Planning.PhasePlanner;

/// <summary>
/// Command handler for the Planner phase of the orchestrator workflow.
/// Coordinates context gathering, task planning, and assumption documentation.
/// </summary>
public sealed class PhasePlannerHandler
{
    private readonly IPhaseContextGatherer _contextGatherer;
    private readonly IPhasePlanner _phasePlanner;
    private readonly IPhaseAssumptionLister _assumptionLister;
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly IEventStore _eventStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhasePlannerHandler"/> class.
    /// </summary>
    public PhasePlannerHandler(
        IPhaseContextGatherer contextGatherer,
        IPhasePlanner phasePlanner,
        IPhaseAssumptionLister assumptionLister,
        IRunLifecycleManager runLifecycleManager,
        IEventStore eventStore)
    {
        _contextGatherer = contextGatherer ?? throw new ArgumentNullException(nameof(contextGatherer));
        _phasePlanner = phasePlanner ?? throw new ArgumentNullException(nameof(phasePlanner));
        _assumptionLister = assumptionLister ?? throw new ArgumentNullException(nameof(assumptionLister));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <summary>
    /// Handles the planner phase command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            // Extract phase ID from request inputs
            var phaseId = ExtractPhaseId(request);
            if (string.IsNullOrEmpty(phaseId))
            {
                return CommandRouteResult.Failure(1, "Phase ID is required for planning.");
            }

            // Step 1: Gather context for the phase
            var brief = await _contextGatherer.GatherContextAsync(phaseId, runId, ct);

            // Step 2: Create task plan from the brief
            var taskPlan = await _phasePlanner.CreateTaskPlanAsync(brief, runId, ct);

            if (!taskPlan.IsValid)
            {
                return CommandRouteResult.Failure(
                    2,
                    $"Task planning failed for phase {phaseId}: {string.Join(", ", taskPlan.ValidationErrors)}");
            }

            // Step 3: Extract and document assumptions
            var assumptions = await _assumptionLister.ExtractAssumptionsAsync(brief, taskPlan, runId, ct);
            var assumptionsPath = await _assumptionLister.GenerateAssumptionsDocumentAsync(assumptions, runId, ct);

            var completedAt = DateTimeOffset.UtcNow;

            return CommandRouteResult.Success(
                $"Phase planning completed successfully for {phaseId}. " +
                $"Created {taskPlan.Tasks.Count} task(s). " +
                $"Assumptions documented at: {assumptionsPath}");
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Phase planner handler failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the full phase planning workflow and returns detailed results.
    /// </summary>
    /// <param name="phaseId">The phase identifier to plan.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detailed phase planning result.</returns>
    public async Task<PhasePlannerResult> PlanPhaseAsync(string phaseId, string runId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(phaseId);

        var startedAt = DateTimeOffset.UtcNow;
        var artifacts = new List<PlanningArtifact>();

        try
        {
            // Step 1: Gather context for the phase
            var brief = await _contextGatherer.GatherContextAsync(phaseId, runId, ct);

            // Step 2: Create task plan from the brief
            var taskPlan = await _phasePlanner.CreateTaskPlanAsync(brief, runId, ct);

            if (!taskPlan.IsValid)
            {
                return new PhasePlannerResult
                {
                    IsSuccess = false,
                    PhaseId = phaseId,
                    PhaseBrief = brief,
                    ErrorMessage = $"Task planning failed: {string.Join(", ", taskPlan.ValidationErrors)}",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow
                };
            }

            // Step 3: Extract and document assumptions
            var assumptions = await _assumptionLister.ExtractAssumptionsAsync(brief, taskPlan, runId, ct);
            var assumptionsPath = await _assumptionLister.GenerateAssumptionsDocumentAsync(assumptions, runId, ct);

            // Step 4: Persist planning decision to events.ndjson
            PersistPlanningDecision(phaseId, taskPlan, runId);

            var completedAt = DateTimeOffset.UtcNow;

            return new PhasePlannerResult
            {
                IsSuccess = true,
                PhaseId = phaseId,
                PhaseBrief = brief,
                TaskPlan = taskPlan,
                Assumptions = assumptions,
                AssumptionsDocumentPath = assumptionsPath,
                StartedAt = startedAt,
                CompletedAt = completedAt,
                Artifacts = artifacts.AsReadOnly()
            };
        }
        catch (Exception ex)
        {
            return new PhasePlannerResult
            {
                IsSuccess = false,
                PhaseId = phaseId,
                ErrorMessage = ex.Message,
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
    }

    private static string ExtractPhaseId(CommandRequest request)
    {
        // Try to extract from options if present
        if (request.Options.TryGetValue("phase-id", out var phaseIdValue) && !string.IsNullOrEmpty(phaseIdValue))
        {
            return phaseIdValue;
        }

        // Try to extract from arguments if present
        foreach (var arg in request.Arguments)
        {
            if (arg.StartsWith("--phase-id=", StringComparison.OrdinalIgnoreCase))
            {
                return arg[11..];
            }
        }

        // Default: return a placeholder that will need to be handled by the caller
        return string.Empty;
    }

    private void PersistPlanningDecision(string phaseId, TaskPlan taskPlan, string runId)
    {
        try
        {
            var eventPayload = new
            {
                eventType = "phase.planned",
                timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                runId = runId,
                data = new
                {
                    phaseId = phaseId,
                    planId = taskPlan.PlanId,
                    taskCount = taskPlan.Tasks.Count,
                    tasks = taskPlan.Tasks.Select(t => new
                    {
                        taskId = t.TaskId,
                        title = t.Title,
                        fileScopeCount = t.FileScopes.Count
                    }).ToList()
                }
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var jsonElement = JsonSerializer.SerializeToElement(eventPayload, jsonOptions);
            _eventStore.AppendEvent(jsonElement);
        }
        catch
        {
            // Event persistence failures should not fail the planning workflow
        }
    }
}
