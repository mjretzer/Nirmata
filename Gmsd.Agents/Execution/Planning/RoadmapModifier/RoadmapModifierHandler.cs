using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public;
using System.Text.Json;

namespace Gmsd.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Command handler for roadmap modification operations.
/// Coordinates phase insertion, removal, and renumbering with safety checks and event emission.
/// </summary>
public sealed class RoadmapModifierHandler
{
    private readonly IRoadmapModifier _roadmapModifier;
    private readonly IRunLifecycleManager _runLifecycleManager;
    private readonly IEventStore _eventStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoadmapModifierHandler"/> class.
    /// </summary>
    public RoadmapModifierHandler(
        IRoadmapModifier roadmapModifier,
        IRunLifecycleManager runLifecycleManager,
        IEventStore eventStore)
    {
        _roadmapModifier = roadmapModifier ?? throw new ArgumentNullException(nameof(roadmapModifier));
        _runLifecycleManager = runLifecycleManager ?? throw new ArgumentNullException(nameof(runLifecycleManager));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <summary>
    /// Handles the roadmap modification command.
    /// </summary>
    /// <param name="request">The command request containing modification parameters.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The command route result.</returns>
    public async Task<CommandRouteResult> HandleAsync(CommandRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            // Parse operation type from command
            var operation = ExtractOperation(request);
            if (operation == null)
            {
                return CommandRouteResult.Failure(1, "Operation type is required (insert, remove, or renumber).");
            }

            // Build modification request based on operation
            RoadmapModifyResult result;

            switch (operation.Value)
            {
                case RoadmapModifyOperation.Insert:
                    var insertRequest = BuildInsertRequest(request);
                    if (insertRequest == null)
                    {
                        return CommandRouteResult.Failure(2, "Invalid insert request parameters.");
                    }
                    result = await _roadmapModifier.InsertPhaseAsync(insertRequest, runId, ct);
                    break;

                case RoadmapModifyOperation.Remove:
                    var (phaseId, force) = ExtractRemoveParameters(request);
                    if (string.IsNullOrEmpty(phaseId))
                    {
                        return CommandRouteResult.Failure(3, "Phase ID is required for remove operation.");
                    }
                    result = await _roadmapModifier.RemovePhaseAsync(phaseId, force, runId, ct);
                    break;

                case RoadmapModifyOperation.Renumber:
                    result = await _roadmapModifier.RenumberPhasesAsync(runId, ct);
                    break;

                default:
                    return CommandRouteResult.Failure(4, $"Unsupported operation: {operation.Value}");
            }

            // Handle result
            if (result.IsBlocked)
            {
                return CommandRouteResult.Failure(
                    5,
                    $"Roadmap modification blocked: {result.BlockerReason}. Issue created: {result.BlockerIssueId}");
            }

            if (!result.IsSuccess)
            {
                return CommandRouteResult.Failure(
                    6,
                    $"Roadmap modification failed: {result.ErrorMessage}");
            }

            // Record command to lifecycle manager
            await _runLifecycleManager.RecordCommandAsync(
                runId,
                "roadmap",
                operation.Value.ToString().ToLowerInvariant(),
                "completed",
                ct);

            var message = operation.Value switch
            {
                RoadmapModifyOperation.Insert => $"Phase {result.AffectedPhaseId} inserted successfully.",
                RoadmapModifyOperation.Remove => $"Phase {result.AffectedPhaseId} removed successfully.",
                RoadmapModifyOperation.Renumber => "Phases renumbered successfully.",
                _ => "Roadmap modified successfully."
            };

            return CommandRouteResult.Success(message);
        }
        catch (Exception ex)
        {
            return CommandRouteResult.Failure(99, $"Roadmap modifier handler failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes the roadmap modification workflow and returns detailed results.
    /// </summary>
    /// <param name="request">The modification request.</param>
    /// <param name="runId">The current run identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Detailed modification result.</returns>
    public async Task<RoadmapModifyResult> ModifyAsync(RoadmapModifyRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Operation switch
        {
            RoadmapModifyOperation.Insert => await _roadmapModifier.InsertPhaseAsync(request, runId, ct),
            RoadmapModifyOperation.Remove => await _roadmapModifier.RemovePhaseAsync(
                request.ReferencePhaseId ?? string.Empty,
                request.Force,
                runId,
                ct),
            RoadmapModifyOperation.Renumber => await _roadmapModifier.RenumberPhasesAsync(runId, ct),
            _ => RoadmapModifyResult.FailedResult($"Unsupported operation: {request.Operation}", "UNSUPPORTED_OPERATION")
        };
    }

    private static RoadmapModifyOperation? ExtractOperation(CommandRequest request)
    {
        // Try to extract from options
        if (request.Options.TryGetValue("operation", out var operationValue))
        {
            if (Enum.TryParse<RoadmapModifyOperation>(operationValue, true, out var operation))
            {
                return operation;
            }
        }

        // Try to extract from command name
        var commandName = request.Command.ToLowerInvariant();
        if (commandName.Contains("insert") || commandName.Contains("add"))
        {
            return RoadmapModifyOperation.Insert;
        }
        if (commandName.Contains("remove") || commandName.Contains("delete"))
        {
            return RoadmapModifyOperation.Remove;
        }
        if (commandName.Contains("renumber") || commandName.Contains("reorder"))
        {
            return RoadmapModifyOperation.Renumber;
        }

        return null;
    }

    private static RoadmapModifyRequest? BuildInsertRequest(CommandRequest request)
    {
        var insertRequest = new RoadmapModifyRequest
        {
            Operation = RoadmapModifyOperation.Insert,
            Position = InsertPosition.AtEnd
        };

        // Extract phase name
        if (request.Options.TryGetValue("name", out var name))
        {
            insertRequest.NewPhaseName = name;
        }

        // Extract phase description
        if (request.Options.TryGetValue("description", out var description))
        {
            insertRequest.NewPhaseDescription = description;
        }

        // Extract target milestone
        if (request.Options.TryGetValue("milestone-id", out var milestoneId))
        {
            insertRequest.TargetMilestoneId = milestoneId;
        }

        // Extract reference phase and position
        if (request.Options.TryGetValue("after", out var afterPhaseId))
        {
            insertRequest.ReferencePhaseId = afterPhaseId;
            insertRequest.Position = InsertPosition.After;
        }
        else if (request.Options.TryGetValue("before", out var beforePhaseId))
        {
            insertRequest.ReferencePhaseId = beforePhaseId;
            insertRequest.Position = InsertPosition.Before;
        }
        else if (request.Options.TryGetValue("position", out var positionValue))
        {
            if (Enum.TryParse<InsertPosition>(positionValue, true, out var position))
            {
                insertRequest.Position = position;
            }
        }

        // Validate required fields
        if (string.IsNullOrEmpty(insertRequest.NewPhaseName))
        {
            return null;
        }

        return insertRequest;
    }

    private static (string phaseId, bool force) ExtractRemoveParameters(CommandRequest request)
    {
        string phaseId = string.Empty;
        bool force = false;

        // Extract phase ID
        if (request.Options.TryGetValue("phase-id", out var phaseIdValue) && phaseIdValue != null)
        {
            phaseId = phaseIdValue;
        }

        // Extract force flag
        if (request.Options.TryGetValue("force", out var forceValue) && forceValue != null)
        {
            force = forceValue.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        return (phaseId ?? string.Empty, force);
    }
}
