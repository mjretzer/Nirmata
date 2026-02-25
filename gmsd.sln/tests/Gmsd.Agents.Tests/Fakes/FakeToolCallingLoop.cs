using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gmsd.Agents.Execution.ToolCalling;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Minimal fake implementation of <see cref="IToolCallingLoop"/> used by HandlerTestHost.
/// Always returns a completed result without exercising real tool-calling behavior.
/// </summary>
public sealed class FakeToolCallingLoop : IToolCallingLoop
{
    public Task<ToolCallingResult> ExecuteAsync(ToolCallingRequest request, CancellationToken cancellationToken = default)
    {
        var result = new ToolCallingResult
        {
            FinalMessage = ToolCallingMessage.Assistant("Tool calling not configured in tests."),
            ConversationHistory = new List<ToolCallingMessage>
            {
                ToolCallingMessage.System("Tool calling loop stubbed for tests."),
            },
            IterationCount = 0,
            CompletionReason = ToolCallingCompletionReason.CompletedNaturally,
            Usage = new ToolCallingUsageStats
            {
                IterationCount = 0,
                TotalCompletionTokens = 0,
                TotalPromptTokens = 0
            }
        };

        return Task.FromResult(result);
    }
}
