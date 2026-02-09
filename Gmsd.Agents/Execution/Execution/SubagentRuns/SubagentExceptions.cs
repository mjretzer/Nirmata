namespace Gmsd.Agents.Execution.Execution.SubagentRuns;

/// <summary>
/// Exception thrown when the subagent execution budget is exceeded.
/// </summary>
public sealed class SubagentBudgetExceededException : Exception
{
    public SubagentBudgetExceededException(string message) : base(message) { }
    public SubagentBudgetExceededException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Exception thrown when a file scope violation is detected.
/// </summary>
public sealed class SubagentScopeViolationException : Exception
{
    public string TargetPath { get; }
    public IReadOnlyList<string> AllowedScopes { get; }

    public SubagentScopeViolationException(string message, string targetPath, IReadOnlyList<string> allowedScopes)
        : base(message)
    {
        TargetPath = targetPath;
        AllowedScopes = allowedScopes;
    }
}

/// <summary>
/// Exception thrown when subagent execution times out.
/// </summary>
public sealed class SubagentTimeoutException : Exception
{
    public int TimeoutSeconds { get; }

    public SubagentTimeoutException(string message, int timeoutSeconds) : base(message)
    {
        TimeoutSeconds = timeoutSeconds;
    }
}

/// <summary>
/// Exception thrown when context pack loading fails.
/// </summary>
public sealed class SubagentContextLoadException : Exception
{
    public string? ContextPackId { get; }

    public SubagentContextLoadException(string message, string? contextPackId = null) : base(message)
    {
        ContextPackId = contextPackId;
    }

    public SubagentContextLoadException(string message, Exception inner, string? contextPackId = null)
        : base(message, inner)
    {
        ContextPackId = contextPackId;
    }
}
