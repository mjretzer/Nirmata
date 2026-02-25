using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Gmsd.Agents.Execution.ControlPlane.Chat;
using Gmsd.Agents.Execution.ControlPlane.Chat.Models;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Simple fake chat responder that returns canned responses for integration tests.
/// </summary>
public sealed class FakeChatResponder : IChatResponder
{
    public Task<ChatResponse> RespondAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var response = new ChatResponse
        {
            Content = request.Input ?? "(no input)",
            IsSuccess = true,
            Model = "fake",
            PromptTokens = 1,
            CompletionTokens = 1,
            DurationMs = 0
        };

        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatDelta> StreamResponseAsync(
        ChatRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new ChatDelta
        {
            Content = request.Input,
            IsComplete = false
        };

        await Task.CompletedTask;

        yield return new ChatDelta
        {
            Content = null,
            IsComplete = true
        };
    }
}
