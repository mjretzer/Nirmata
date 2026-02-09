namespace Gmsd.Agents.Execution.ControlPlane;

/// <summary>
/// Gating engine implementation that evaluates workspace state and determines the appropriate workflow phase.
/// </summary>
public sealed class GatingEngine : IGatingEngine
{
    /// <inheritdoc />
    public Task<GatingResult> EvaluateAsync(GatingContext context, CancellationToken ct = default)
    {
        // Gating decisions based on documented state machine priority.
        // See: openspec/specs/agents-orchestrator-workflow/spec.md

        // 1. Missing project spec -> Interviewer
        if (!context.HasProject)
        {
            return Task.FromResult(new GatingResult
            {
                TargetPhase = "Interviewer",
                Reason = "Project specification not found",
                ContextData = new Dictionary<string, object> { { "hasProject", false } }
            });
        }

        // 2. Missing roadmap -> Roadmapper
        if (!context.HasRoadmap)
        {
            return Task.FromResult(new GatingResult
            {
                TargetPhase = "Roadmapper",
                Reason = "Roadmap not defined for project",
                ContextData = new Dictionary<string, object> { { "hasRoadmap", false } }
            });
        }

        // 3. Missing phase plan for current cursor -> Planner
        if (!context.HasPlan)
        {
            return Task.FromResult(new GatingResult
            {
                TargetPhase = "Planner",
                Reason = "No plan exists for current cursor position",
                ContextData = new Dictionary<string, object> { { "hasPlan", false }, { "cursor", context.CurrentCursor ?? "null" } }
            });
        }

        // 4. Verification failed -> FixPlanner
        if (context.LastVerificationStatus == "failed")
        {
            return Task.FromResult(new GatingResult
            {
                TargetPhase = "FixPlanner",
                Reason = "Verification failed, fix planning required",
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
            return Task.FromResult(new GatingResult
            {
                TargetPhase = "FixPlanner",
                Reason = "Task execution failed, recovery required",
                ContextData = new Dictionary<string, object>
                {
                    { "executionStatus", "failed" },
                    { "parentTaskId", context.ParentTaskId ?? "unknown" }
                }
            });
        }

        // 6. Execution completed, verification pending -> Verifier
        if (context.LastExecutionStatus == "completed" && context.LastVerificationStatus != "passed")
        {
            return Task.FromResult(new GatingResult
            {
                TargetPhase = "Verifier",
                Reason = "Execution complete, awaiting verification",
                ContextData = new Dictionary<string, object> { { "executionStatus", "completed" } }
            });
        }

        // 7. Default: No specific phase triggered, respond conversationally
        return Task.FromResult(new GatingResult
        {
            TargetPhase = "Responder",
            Reason = "No specific workflow triggered, defaulting to conversational response.",
            ContextData = new Dictionary<string, object> { { "cursor", context.CurrentCursor ?? "null" } }
        });
    }
}
