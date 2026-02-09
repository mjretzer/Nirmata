using Gmsd.Agents.Execution.Execution.SubagentRuns;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of ISubagentOrchestrator for testing.
/// Returns a canned subagent run result for testing purposes.
/// </summary>
public sealed class FakeSubagentOrchestrator : ISubagentOrchestrator
{
    /// <summary>
    /// The canned result to return. Configure this before testing.
    /// </summary>
    public SubagentRunResult? CannedResult { get; set; }

    /// <summary>
    /// Gets the last request that was passed to RunSubagentAsync.
    /// Useful for test assertions.
    /// </summary>
    public SubagentRunRequest? LastRequest { get; private set; }

    /// <summary>
    /// Runs a subagent and returns a canned result.
    /// </summary>
    public Task<SubagentRunResult> RunSubagentAsync(SubagentRunRequest request, CancellationToken ct = default)
    {
        LastRequest = request;

        if (CannedResult != null)
        {
            return Task.FromResult(CannedResult);
        }

        // Return default success result
        return Task.FromResult(new SubagentRunResult
        {
            Success = true,
            RunId = request.RunId,
            TaskId = request.TaskId,
            NormalizedOutput = "Subagent execution completed successfully.",
            DeterministicHash = "abc123",
            ModifiedFiles = Array.Empty<string>(),
            EvidenceArtifacts = new[] { $"{request.RunId}/summary.json" },
            ToolCalls = Array.Empty<SubagentToolCall>(),
            Metrics = new SubagentExecutionMetrics
            {
                IterationCount = 1,
                ToolCallCount = 0,
                ExecutionTimeMs = 100,
                TokensConsumed = 0
            }
        });
    }
}
