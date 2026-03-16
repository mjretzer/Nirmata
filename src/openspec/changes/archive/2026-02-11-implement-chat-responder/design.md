# Design: Real Chat Responder Implementation

## Architecture Overview

The chat responder implementation follows the existing engine patterns while extending the conversational capabilities of the orchestrator.

```
┌─────────────────────────────────────────────────────────────────┐
│                        Orchestrator                              │
│  ┌──────────────┐  ┌─────────────┐  ┌──────────────────────┐  │
│  │ InputClassifier│→ │ SideEffect. │→ │   ChatResponder      │  │
│  │                │  │    None     │  │  (LLM-backed)        │  │
│  └──────────────┘  └─────────────┘  └──────────────────────┘  │
│                                                │                 │
│                           ┌────────────────────┘                 │
│                           ↓                                     │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    IChatResponder                        │  │
│  │  ┌────────────┐  ┌────────────┐  ┌──────────────────┐ │  │
│  │  │   Chat     │  │  Context   │  │   Response       │ │  │
│  │  │   Prompt   │  │  Assembly  │  │   Formatter      │ │  │
│  │  │   Builder  │  │            │  │                  │ │  │
│  │  └────────────┘  └────────────┘  └──────────────────┘ │  │
│  └──────────────────────────────────────────────────────────┘  │
│                           │                                     │
│                           ↓                                     │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │                    ILlmProvider                          │  │
│  │            (OpenAI, Anthropic, Ollama)                   │  │
│  └──────────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

## Key Components

### 1. IChatResponder Interface

Replaces the concrete `ChatResponder` class to allow for:
- Multiple implementations (streaming vs. blocking)
- Test fakes for unit testing
- Future extensions (negotiation mode, tool-augmented chat)

```csharp
public interface IChatResponder
{
    /// <summary>
    /// Generates a chat response using the LLM provider.
    /// </summary>
    Task<ChatResponse> RespondAsync(
        ChatRequest request, 
        CancellationToken ct = default);
    
    /// <summary>
    /// Streams a chat response token-by-token.
    /// </summary>
    IAsyncEnumerable<ChatDelta> StreamResponseAsync(
        ChatRequest request, 
        CancellationToken ct = default);
}
```

### 2. ChatContextAssembly

Assembles context for the LLM prompt:
- Current workspace state (cursor position, active phase)
- Available specs (project, roadmap, phase plans - summaries only)
- Available commands with `/help` descriptions
- Recent run history (last 3 runs for continuity)

Context is bounded to prevent token bloat:
- Max 2000 tokens for context assembly
- File contents truncated if too large
- Spec summaries, not full JSON

### 3. ChatPromptBuilder

Constructs the system prompt for chat mode:

```
You are nirmata Assistant, an AI orchestration agent helping users with software project planning and execution.

CURRENT WORKSPACE STATE:
- Project: {projectName} (defined: {hasProject})
- Roadmap: {roadmapPhaseCount} phases defined
- Current position: {cursorPhase}/{cursorTask}
- Last run: {lastRunStatus}

AVAILABLE COMMANDS:
- /help - Show available commands
- /status - Show workspace status
- /run --task-id <id> - Execute a specific task
- /plan --phase-id <id> - Plan a phase

USER INPUT: {userInput}

Respond conversationally. If the user is asking for something that requires a command, suggest the appropriate command but do not execute it unless explicitly confirmed.
```

### 4. LlmChatResponder (Implementation)

The concrete implementation using `ILlmProvider`:

```csharp
public sealed class LlmChatResponder : IChatResponder
{
    private readonly ILlmProvider _llmProvider;
    private readonly IChatContextAssembly _contextAssembly;
    private readonly ChatPromptBuilder _promptBuilder;
    
    public async Task<ChatResponse> RespondAsync(ChatRequest request, CancellationToken ct)
    {
        var context = await _contextAssembly.AssembleAsync(ct);
        var prompt = _promptBuilder.Build(request.Input, context);
        
        var llmRequest = new LlmCompletionRequest
        {
            Messages = new[]
            {
                new LlmMessage { Role = "system", Content = prompt.System },
                new LlmMessage { Role = "user", Content = prompt.User }
            },
            MaxTokens = 1000,
            Temperature = 0.7
        };
        
        var response = await _llmProvider.CompleteAsync(llmRequest, ct);
        return new ChatResponse { Content = response.Content };
    }
    
    public async IAsyncEnumerable<ChatDelta> StreamResponseAsync(
        ChatRequest request, 
        [EnumeratorCancellation] CancellationToken ct)
    {
        var context = await _contextAssembly.AssembleAsync(ct);
        var prompt = _promptBuilder.Build(request.Input, context);
        
        var llmRequest = new LlmCompletionRequest { /* ... */ };
        
        await foreach (var delta in _llmProvider.StreamCompletionAsync(llmRequest, ct))
        {
            yield return new ChatDelta { Content = delta.Content };
        }
    }
}
```

## Integration Points

### Orchestrator.cs Changes

Replace direct `ChatResponder` instantiation with `IChatResponder`:

```csharp
// OLD (line 100):
return _chatResponder.Respond(intent.InputRaw);

// NEW:
var chatRequest = new ChatRequest 
{ 
    Input = intent.InputRaw,
    IncludeWorkspaceContext = true 
};

var response = await _chatResponder.RespondAsync(chatRequest, ct);

return new OrchestratorResult
{
    IsSuccess = true,
    FinalPhase = "Chat",
    Artifacts = new Dictionary<string, object>
    {
        ["response"] = response.Content,
        ["model"] = response.Model,
        ["tokensUsed"] = response.TotalTokens
    }
};
```

### GatingEngine → ResponderHandler Path

When the gating engine selects the "Responder" phase (line 90-95 in GatingEngine.cs), the `ResponderHandler` should also use the LLM-backed responder:

```csharp
public sealed class ResponderHandler
{
    private readonly IChatResponder _chatResponder;
    
    public async Task<CommandRouteResult> HandleAsync(
        CommandRequest request, 
        string runId, 
        CancellationToken ct)
    {
        // Use the LLM responder for conversational "Responder" phase
        var chatRequest = new ChatRequest 
        { 
            Input = request.Options.GetValueOrDefault("message", "What can you do?"),
            IncludeWorkspaceContext = true
        };
        
        var response = await _chatResponder.RespondAsync(chatRequest, ct);
        
        return CommandRouteResult.Success(response.Content);
    }
}
```

## Streaming Strategy

The chat responder supports two modes:

1. **Blocking Mode**: Used by default for simple responses
   - Complete LLM call → single response
   - Lower latency for short answers

2. **Streaming Mode**: Used when the UI requests SSE streaming
   - Tokens emitted as they arrive from the LLM
   - Better UX for longer responses

The streaming protocol uses the existing SSE infrastructure in `ChatStreamingController`.

## Error Handling

- **LLM Provider Unavailable**: Return friendly error message + fallback to cached help text
- **Context Assembly Fails**: Degrade gracefully (chat without workspace context)
- **Timeout (>10s)**: Return timeout message with suggestion to retry
- **Rate Limit**: Queue and retry with backoff

## Testing Strategy

1. **Unit Tests**: Fake `ILlmProvider` returning predetermined responses
2. **Integration Tests**: In-memory Ollama or mocked HTTP handler
3. **E2E Tests**: Full flow from HTTP POST to SSE response

## Performance Considerations

- Context assembly cached for 5 seconds (workspace state changes infrequently)
- LLM calls respect provider rate limits
- Streaming starts within 500ms of request
- Non-streaming completes within 3 seconds for typical queries
