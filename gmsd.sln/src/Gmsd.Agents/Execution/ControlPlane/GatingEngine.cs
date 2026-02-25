namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Gating engine implementation that evaluates workspace state and determines the appropriate workflow phase.
/// </summary>
public sealed class GatingEngine : IGatingEngine
{
    private readonly IDestructivenessAnalyzer _destructivenessAnalyzer;
    private readonly IConfirmationGateEvaluator? _confirmationEvaluator;

    /// <summary>
    /// Initializes a new instance of the <see cref="GatingEngine"/> class.
    /// </summary>
    /// <param name="destructivenessAnalyzer">The destructiveness analyzer.</param>
    public GatingEngine(IDestructivenessAnalyzer destructivenessAnalyzer)
    {
        _destructivenessAnalyzer = destructivenessAnalyzer;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GatingEngine"/> class with confirmation evaluator.
    /// </summary>
    /// <param name="destructivenessAnalyzer">The destructiveness analyzer.</param>
    /// <param name="confirmationEvaluator">The confirmation gate evaluator.</param>
    public GatingEngine(
        IDestructivenessAnalyzer destructivenessAnalyzer,
        IConfirmationGateEvaluator confirmationEvaluator)
    {
        _destructivenessAnalyzer = destructivenessAnalyzer;
        _confirmationEvaluator = confirmationEvaluator;
    }
    /// <inheritdoc />
    public Task<GatingResult> EvaluateAsync(GatingContext context, CancellationToken ct = default)
    {
        // Gating decisions based on documented state machine priority.
        // See: openspec/specs/agents-orchestrator-workflow/spec.md

        // 1. Missing project spec -> Interviewer
        if (!context.HasProject)
        {
            var phase = "Interviewer";
            var reasoning = $"Routing to {phase} because project specification is missing. This is the initial setup phase.";
            var requiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(phase, context);
            var riskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, context);
            var sideEffects = _destructivenessAnalyzer.GetSideEffects(phase);

            return Task.FromResult(new GatingResult
            {
                TargetPhase = phase,
                Reason = "Project specification not found",
                Reasoning = reasoning,
                RequiresConfirmation = requiresConfirmation,
                ProposedAction = new ProposedAction
                {
                    Phase = phase,
                    Description = "Initialize a new project specification through user interview",
                    RiskLevel = riskLevel,
                    SideEffects = sideEffects,
                    AffectedResources = new[] { ".aos/project-spec.json" }
                },
                ContextData = new Dictionary<string, object> { { "hasProject", false } }
            });
        }

        // 2. Missing roadmap -> Roadmapper
        if (!context.HasRoadmap)
        {
            var phase = "Roadmapper";
            var reasoning = $"Routing to {phase} because project roadmap is not defined. Next step after project specification.";
            var requiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(phase, context);
            var riskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, context);
            var sideEffects = _destructivenessAnalyzer.GetSideEffects(phase);

            return Task.FromResult(new GatingResult
            {
                TargetPhase = phase,
                Reason = "Roadmap not defined for project",
                Reasoning = reasoning,
                RequiresConfirmation = requiresConfirmation,
                ProposedAction = new ProposedAction
                {
                    Phase = phase,
                    Description = "Create a project roadmap with phases and milestones",
                    RiskLevel = riskLevel,
                    SideEffects = sideEffects,
                    AffectedResources = new[] { ".aos/roadmap.json" }
                },
                ContextData = new Dictionary<string, object> { { "hasRoadmap", false } }
            });
        }

        // 3. Missing phase plan for current cursor -> Planner
        if (!context.HasPlan)
        {
            var phase = "Planner";
            var reasoning = $"Routing to {phase} because no plan exists for current cursor position '{context.CurrentCursor ?? "null"}'. Need to plan the work at this position.";
            var requiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(phase, context);
            var riskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, context);
            var sideEffects = _destructivenessAnalyzer.GetSideEffects(phase);

            return Task.FromResult(new GatingResult
            {
                TargetPhase = phase,
                Reason = "No plan exists for current cursor position",
                Reasoning = reasoning,
                RequiresConfirmation = requiresConfirmation,
                ProposedAction = new ProposedAction
                {
                    Phase = phase,
                    Description = $"Create an execution plan for the work at cursor position: {context.CurrentCursor ?? "unknown"}",
                    RiskLevel = riskLevel,
                    SideEffects = sideEffects,
                    AffectedResources = new[] { ".aos/plans/" }
                },
                ContextData = new Dictionary<string, object> { { "hasPlan", false }, { "cursor", context.CurrentCursor ?? "null" } }
            });
        }

        // 4. Verification failed -> FixPlanner
        if (context.LastVerificationStatus == "failed")
        {
            var phase = "FixPlanner";
            var reasoning = $"Routing to {phase} because verification failed. Need to create a fix plan to address the issues.";
            var requiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(phase, context);
            var riskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, context);
            var sideEffects = _destructivenessAnalyzer.GetSideEffects(phase);

            return Task.FromResult(new GatingResult
            {
                TargetPhase = phase,
                Reason = "Verification failed, fix planning required",
                Reasoning = reasoning,
                RequiresConfirmation = requiresConfirmation,
                ProposedAction = new ProposedAction
                {
                    Phase = phase,
                    Description = "Create a fix plan to address verification failures",
                    RiskLevel = riskLevel,
                    SideEffects = sideEffects,
                    AffectedResources = new[] { ".aos/plans/", ".aos/issues/" }
                },
                ContextData = new Dictionary<string, object>
                {
                    { "verificationStatus", "failed" },
                    { "parentTaskId", context.ParentTaskId ?? "unknown" },
                    { "issueIds", context.IssueIds }
                }
            });
        }

        // 5. Execution failed -> FixPlanner (for recovery)
        if (context.LastExecutionStatus == "failed")
        {
            var phase = "FixPlanner";
            var reasoning = $"Routing to {phase} because task execution failed. Need to create a recovery plan.";
            var requiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(phase, context);
            var riskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, context);
            var sideEffects = _destructivenessAnalyzer.GetSideEffects(phase);

            return Task.FromResult(new GatingResult
            {
                TargetPhase = phase,
                Reason = "Task execution failed, recovery required",
                Reasoning = reasoning,
                RequiresConfirmation = requiresConfirmation,
                ProposedAction = new ProposedAction
                {
                    Phase = phase,
                    Description = "Create a recovery plan for the failed task execution",
                    RiskLevel = riskLevel,
                    SideEffects = sideEffects,
                    AffectedResources = new[] { ".aos/plans/" }
                },
                ContextData = new Dictionary<string, object>
                {
                    { "executionStatus", "failed" },
                    { "parentTaskId", context.ParentTaskId ?? "unknown" }
                }
            });
        }

        // 6. Ready to execute (no pending execution or verification issues)
        // This includes: fresh ready state OR verification passed (continue to next execution)
        if (context.LastExecutionStatus == null && context.LastVerificationStatus == null ||
            context.LastVerificationStatus == "passed")
        {
            var executorPhase = "Executor";
            var executorReasoning = $"Routing to {executorPhase} because the system is ready to execute the plan at cursor '{context.CurrentCursor ?? "null"}'";
            var executorRequiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(executorPhase, context);
            var executorRiskLevel = _destructivenessAnalyzer.AnalyzeRisk(executorPhase, context);
            var executorSideEffects = _destructivenessAnalyzer.GetSideEffects(executorPhase);

            return Task.FromResult(new GatingResult
            {
                TargetPhase = executorPhase,
                Reason = "Ready to execute the plan",
                Reasoning = executorReasoning,
                RequiresConfirmation = executorRequiresConfirmation,
                ProposedAction = new ProposedAction
                {
                    Phase = executorPhase,
                    Description = "Execute the planned tasks",
                    RiskLevel = executorRiskLevel,
                    SideEffects = executorSideEffects,
                    AffectedResources = new[] { "workspace_files" }
                },
                ContextData = new Dictionary<string, object> { { "cursor", context.CurrentCursor ?? "null" } }
            });
        }

        // 7. Execution completed, verification pending -> Verifier
        if (context.LastExecutionStatus == "completed" && context.LastVerificationStatus != "passed")
        {
            var phase = "Verifier";
            var reasoning = $"Routing to {phase} because execution completed and verification is pending. Need to verify the work.";
            var requiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(phase, context);
            var riskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, context);
            var sideEffects = _destructivenessAnalyzer.GetSideEffects(phase);

            return Task.FromResult(new GatingResult
            {
                TargetPhase = phase,
                Reason = "Execution complete, awaiting verification",
                Reasoning = reasoning,
                RequiresConfirmation = requiresConfirmation,
                ProposedAction = new ProposedAction
                {
                    Phase = phase,
                    Description = "Run verification checks on the completed execution",
                    RiskLevel = riskLevel,
                    SideEffects = sideEffects,
                    AffectedResources = Array.Empty<string>()
                },
                ContextData = new Dictionary<string, object> { { "executionStatus", "completed" } }
            });
        }

        // 8. Default: No specific phase triggered, respond conversationally
        var defaultPhase = "Responder";
        var defaultReasoning = $"No specific workflow triggered, defaulting to {defaultPhase}. Cursor at '{context.CurrentCursor ?? "null"}'.";
        var defaultRequiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(defaultPhase, context);
        var defaultRiskLevel = _destructivenessAnalyzer.AnalyzeRisk(defaultPhase, context);
        var defaultSideEffects = _destructivenessAnalyzer.GetSideEffects(defaultPhase);

        return Task.FromResult(new GatingResult
        {
            TargetPhase = defaultPhase,
            Reason = "No specific workflow triggered, defaulting to conversational response.",
            Reasoning = defaultReasoning,
            RequiresConfirmation = defaultRequiresConfirmation,
            ProposedAction = new ProposedAction
            {
                Phase = defaultPhase,
                Description = "Respond conversationally to user input",
                RiskLevel = defaultRiskLevel,
                SideEffects = defaultSideEffects,
                AffectedResources = Array.Empty<string>()
            },
            ContextData = new Dictionary<string, object> { { "cursor", context.CurrentCursor ?? "null" } }
        });
    }
}
