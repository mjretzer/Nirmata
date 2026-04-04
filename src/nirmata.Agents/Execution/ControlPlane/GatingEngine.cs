namespace nirmata.Agents.Execution.ControlPlane;

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
        // Strict gate order (spec-first sequencing).
        // See: docs/workflows/gating.md, openspec/changes/agent-foundation-alignment/specs/agent-foundation/spec.md
        //
        // 1. new-project interview
        // 2. brownfield codebase preflight (non-new workspaces only)
        // 3. roadmap
        // 4. phase/task planning
        // 5. execution completed → verification
        // 6. verification failed → fix loop
        // 7. execution failed → fix recovery
        // 8. verification passed → next-phase progression / milestone completion
        // 9. ready to execute
        // 10. default → responder

        // ── Gate 1: Missing project spec → Interviewer ──
        if (!context.HasProject)
        {
            return Task.FromResult(BuildResult("Interviewer", context,
                reason: "Project specification not found",
                description: "Initialize a new project specification through user interview",
                affectedResources: new[] { ".aos/spec/project.json" },
                contextData: new() { { "hasProject", false } }));
        }

        // ── Gate 2: Brownfield codebase preflight (non-new workspaces) → CodebaseMapper ──
        // Only fires when the next required step is roadmap generation or phase planning.
        // If roadmap and task plans already exist, stale/absent codebase intelligence
        // does not block execution or verification.
        // See: spec requirement "AND the next required step is roadmap generation or phase planning"
        if ((!context.HasCodebaseIntelligence || context.IsCodebaseStale)
            && (!context.HasRoadmap || !context.HasTaskPlan))
        {
            var staleness = context.IsCodebaseStale ? "stale" : "absent";
            return Task.FromResult(BuildResult("CodebaseMapper", context,
                reason: $"Codebase intelligence is {staleness}, brownfield preflight required",
                description: "Scan the repository and build codebase intelligence pack before planning",
                affectedResources: new[] { ".aos/codebase/" },
                contextData: new()
                {
                    { "hasCodebaseIntelligence", context.HasCodebaseIntelligence },
                    { "isCodebaseStale", context.IsCodebaseStale }
                }));
        }

        // ── Gate 3: Missing roadmap → Roadmapper ──
        if (!context.HasRoadmap)
        {
            return Task.FromResult(BuildResult("Roadmapper", context,
                reason: "Roadmap not defined for project",
                description: "Create a project roadmap with phases and milestones",
                affectedResources: new[] { ".aos/spec/roadmap.json" },
                contextData: new() { { "hasRoadmap", false } }));
        }

        // ── Gate 4: Missing task plan for current cursor → Planner ──
        // Task plans (.aos/spec/tasks/{taskId}/plan.json) are the only atomic execution contract.
        // Phase-level planning artifacts do not satisfy the execution gate.
        if (!context.HasTaskPlan)
        {
            return Task.FromResult(BuildResult("Planner", context,
                reason: "No task plan exists for current cursor position",
                description: $"Create task execution plans for the work at cursor position: {context.CurrentCursor ?? "unknown"}",
                affectedResources: new[] { ".aos/spec/tasks/" },
                contextData: new() { { "hasTaskPlan", false }, { "cursor", context.CurrentCursor ?? "null" } }));
        }

        // ── Gate 5: Execution completed, verification pending → Verifier ──
        if (context.LastExecutionStatus == "completed" && context.LastVerificationStatus == null)
        {
            return Task.FromResult(BuildResult("Verifier", context,
                reason: "Execution complete, awaiting verification",
                description: "Run verification checks on the completed execution",
                affectedResources: Array.Empty<string>(),
                contextData: new() { { "executionStatus", "completed" } }));
        }

        // ── Gate 6: Verification failed → FixPlanner ──
        if (context.LastVerificationStatus == "failed")
        {
            return Task.FromResult(BuildResult("FixPlanner", context,
                reason: "Verification failed, fix planning required",
                description: "Create a fix plan to address verification failures",
                affectedResources: new[] { ".aos/spec/tasks/", ".aos/spec/issues/" },
                contextData: new()
                {
                    { "verificationStatus", "failed" },
                    { "parentTaskId", context.ParentTaskId ?? "unknown" },
                    { "issueIds", context.IssueIds }
                }));
        }

        // ── Gate 7: Execution failed → FixPlanner (recovery) ──
        if (context.LastExecutionStatus == "failed")
        {
            return Task.FromResult(BuildResult("FixPlanner", context,
                reason: "Task execution failed, recovery required",
                description: "Create a recovery plan for the failed task execution",
                affectedResources: new[] { ".aos/spec/tasks/" },
                contextData: new()
                {
                    { "executionStatus", "failed" },
                    { "parentTaskId", context.ParentTaskId ?? "unknown" }
                }));
        }

        // ── Gate 8: Verification passed → progression (next-phase / milestone completion / next task) ──
        if (context.LastVerificationStatus == "passed")
        {
            // 8a. All phases in milestone are complete → MilestoneProgression
            if (context.IsMilestoneComplete)
            {
                return Task.FromResult(BuildResult("MilestoneProgression", context,
                    reason: "All phases in current milestone verified, milestone progression required",
                    description: "Record milestone completion and advance to the next milestone",
                    affectedResources: new[] { ".aos/spec/milestones/", ".aos/state/state.json" },
                    contextData: new()
                    {
                        { "milestoneComplete", true },
                        { "cursor", context.CurrentCursor ?? "null" }
                    }));
            }

            // 8b. Current phase complete, more phases remain → Planner (next phase)
            if (context.IsPhaseComplete)
            {
                return Task.FromResult(BuildResult("Planner", context,
                    reason: "Current phase verified, planning next phase",
                    description: "Plan the next phase in the roadmap",
                    affectedResources: new[] { ".aos/spec/tasks/" },
                    contextData: new()
                    {
                        { "phaseComplete", true },
                        { "cursor", context.CurrentCursor ?? "null" }
                    }));
            }

            // 8c. More tasks remain in current phase → Executor (next task)
            return Task.FromResult(BuildResult("Executor", context,
                reason: "Task verified, executing next task in phase",
                description: "Execute the next planned task in the current phase",
                affectedResources: new[] { "workspace_files" },
                contextData: new() { { "verificationPassed", true }, { "cursor", context.CurrentCursor ?? "null" } }));
        }

        // ── Gate 8.5: Fix plan emitted, rerun ready → Executor ──
        if (context.LastExecutionStatus == "fix-planned" && context.LastVerificationStatus == "ready-to-execute")
        {
            return Task.FromResult(BuildResult("Executor", context,
                reason: "Fix tasks are planned and ready to rerun",
                description: "Execute the generated fix-task plan",
                affectedResources: new[] { "workspace_files" },
                contextData: new()
                {
                    { "fixRerun", true },
                    { "cursor", context.CurrentCursor ?? "null" }
                }));
        }

        // ── Gate 9: Ready to execute (fresh state, no pending execution) → Executor ──
        if (context.LastExecutionStatus == null && context.LastVerificationStatus == null)
        {
            return Task.FromResult(BuildResult("Executor", context,
                reason: "Ready to execute the plan",
                description: "Execute the planned tasks",
                affectedResources: new[] { "workspace_files" },
                contextData: new() { { "cursor", context.CurrentCursor ?? "null" } }));
        }

        // ── Gate 10: Default → Responder ──
        return Task.FromResult(BuildResult("Responder", context,
            reason: "No specific workflow triggered, defaulting to conversational response.",
            description: "Respond conversationally to user input",
            affectedResources: Array.Empty<string>(),
            contextData: new() { { "cursor", context.CurrentCursor ?? "null" } }));
    }

    private GatingResult BuildResult(
        string phase,
        GatingContext context,
        string reason,
        string description,
        string[] affectedResources,
        Dictionary<string, object> contextData)
    {
        var reasoning = $"Routing to {phase}: {reason}. Cursor at '{context.CurrentCursor ?? "null"}'.";
        var requiresConfirmation = _destructivenessAnalyzer.RequiresConfirmation(phase, context);
        var riskLevel = _destructivenessAnalyzer.AnalyzeRisk(phase, context);
        var sideEffects = _destructivenessAnalyzer.GetSideEffects(phase);

        return new GatingResult
        {
            TargetPhase = phase,
            Reason = reason,
            Reasoning = reasoning,
            RequiresConfirmation = requiresConfirmation,
            ProposedAction = new ProposedAction
            {
                Phase = phase,
                Description = description,
                RiskLevel = riskLevel,
                SideEffects = sideEffects,
                AffectedResources = affectedResources
            },
            ContextData = contextData
        };
    }
}
