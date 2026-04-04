namespace nirmata.Agents.Execution.ControlPlane;

/// <summary>
/// Default implementation of destructiveness analysis for gating decisions.
/// </summary>
public sealed class DestructivenessAnalyzer : IDestructivenessAnalyzer
{
    /// <inheritdoc />
    public RiskLevel AnalyzeRisk(string phase, GatingContext context)
    {
        return phase switch
        {
            "Interviewer" or "Roadmapper" or "Planner" or "FixPlanner"
                or "CodebaseMapper" or "MilestoneProgression" => RiskLevel.WriteSafe,
            "Executor" => RiskLevel.WriteDestructive,
            "Verifier" => RiskLevel.Read,
            "Responder" => RiskLevel.Read,
            _ => RiskLevel.WriteSafe
        };
    }

    /// <inheritdoc />
    public RiskLevel AnalyzeGitOperationRisk(string operation, IReadOnlyList<string> arguments)
    {
        return operation.ToLowerInvariant() switch
        {
            "commit" => RiskLevel.WriteDestructiveGit,
            "push" => RiskLevel.WriteDestructiveGit,
            "merge" => RiskLevel.WriteDestructiveGit,
            "rebase" => RiskLevel.WriteDestructiveGit,
            "reset" => RiskLevel.WriteDestructiveGit,
            "cherry-pick" => RiskLevel.WriteDestructiveGit,
            "revert" => RiskLevel.WriteDestructive,
            "branch" => RiskLevel.WriteSafe,
            "checkout" => RiskLevel.WriteSafe,
            "stash" => RiskLevel.WriteSafe,
            "tag" => RiskLevel.WriteDestructiveGit,
            _ => RiskLevel.WriteDestructive
        };
    }

    /// <inheritdoc />
    public bool IsGitMutatingOperation(string operation)
    {
        var op = operation.ToLowerInvariant();
        return op is "commit" or "push" or "merge" or "rebase" or "reset" 
                   or "cherry-pick" or "tag" or "force-push" or "delete-branch";
    }

    /// <inheritdoc />
    public bool RequiresConfirmation(string phase, GatingContext context)
    {
        var risk = AnalyzeRisk(phase, context);
        
        // Write-destructive operations always require confirmation
        if (risk == RiskLevel.WriteDestructive)
        {
            return true;
        }

        // Git-destructive operations always require confirmation
        if (risk == RiskLevel.WriteDestructiveGit)
        {
            return true;
        }

        // Workspace-destructive operations always require confirmation
        if (risk == RiskLevel.WorkspaceDestructive)
        {
            return true;
        }

        // Write-safe operations may require confirmation in certain contexts
        if (risk == RiskLevel.WriteSafe && phase is "Planner" or "FixPlanner")
        {
            // Require confirmation for replanning when work has already started
            if (context.LastExecutionStatus == "completed")
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSideEffects(string phase)
    {
        return phase switch
        {
            "Interviewer" => new[] { "file_system" },
            "CodebaseMapper" => new[] { "file_system", "external_process" },
            "Roadmapper" => new[] { "file_system" },
            "Planner" => new[] { "file_system" },
            "Executor" => new[] { "file_system", "external_process" },
            "Verifier" => new[] { "external_process" },
            "FixPlanner" => new[] { "file_system" },
            "MilestoneProgression" => new[] { "file_system" },
            "Responder" => Array.Empty<string>(),
            _ => Array.Empty<string>()
        };
    }
}
