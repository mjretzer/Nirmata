# Developer Guide: Extending the Chat and Command System

## Overview

This guide explains how to extend the unified chat and command system with new commands, tools, and features.

## Adding New Commands

### Step 1: Define the Command

Create a command definition in the `CommandRegistry`:

```csharp
public class MyNewCommand : ICommand
{
    public string Name => "mycommand";
    public string Syntax => "/mycommand [options]";
    public string Description => "Description of what the command does";
    
    public string[] GetArgumentNames() => new[] { "option1", "option2" };
    
    public async Task<CommandResult> ExecuteAsync(string[] args, CancellationToken ct)
    {
        // Implementation
        return new CommandResult { Success = true };
    }
}
```

### Step 2: Register the Command

Add to the command registry in `Program.cs` or service configuration:

```csharp
services.AddSingleton<ICommand>(new MyNewCommand());
```

### Step 3: Add to Help Text

Update the command registry to include help information:

```csharp
commandRegistry.Register(new CommandMetadata
{
    Name = "mycommand",
    Syntax = "/mycommand [options]",
    Description = "Description of what the command does",
    Arguments = new[]
    {
        new ArgumentSchema { Name = "option1", Type = "string", Required = true },
        new ArgumentSchema { Name = "option2", Type = "string", Required = false }
    }
});
```

## Extending Chat Responder

### Adding Read-Only Tools

To add a new read-only tool for the chat responder:

```csharp
public class MyReadOnlyTool : IReadOnlyTool
{
    public string Name => "my_tool";
    public string Description => "What this tool does";
    
    public async Task<object> ExecuteAsync(Dictionary<string, object> arguments, CancellationToken ct)
    {
        // Implementation - must be read-only
        return result;
    }
}
```

Register in `ReadOnlyToolRegistry`:

```csharp
registry.RegisterTool(new MyReadOnlyTool());
```

### Customizing Chat Context

Extend `IChatContextAssembly` to include additional workspace facts:

```csharp
public class ExtendedChatContextAssembly : IChatContextAssembly
{
    public async Task<ChatContext> AssembleAsync(WorkflowIntent intent, CancellationToken ct)
    {
        var context = new ChatContext
        {
            Project = await _projectService.GetCurrentProjectAsync(ct),
            Roadmap = await _roadmapService.GetRoadmapAsync(ct),
            // Add custom facts
            CustomFacts = await _customService.GetFactsAsync(ct)
        };
        
        return context;
    }
}
```

### Customizing System Prompt

Extend `ChatPromptBuilder` to customize the system prompt:

```csharp
public class CustomChatPromptBuilder : ChatPromptBuilder
{
    protected override string BuildSystemPrompt(ChatContext context)
    {
        var basePrompt = base.BuildSystemPrompt(context);
        
        // Add custom instructions
        var customInstructions = @"
CUSTOM GUIDELINES:
- Always consider the project's specific requirements
- Reference the custom facts when relevant
";
        
        return basePrompt + "\n" + customInstructions;
    }
}
```

## Implementing Command Suggestions

### Custom Suggestion Detection

Extend `CommandSuggestionDetector` for domain-specific suggestions:

```csharp
public class CustomSuggestionDetector : CommandSuggestionDetector
{
    protected override CommandSuggestion? AnalyzeForCommandSuggestion(string input, string detectedKeyword)
    {
        var baseSuggestion = base.AnalyzeForCommandSuggestion(input, detectedKeyword);
        
        // Add custom logic
        if (input.Contains("specific-keyword"))
        {
            return new CommandSuggestion
            {
                CommandName = "custom",
                Arguments = ExtractCustomArguments(input),
                Confidence = 0.85,
                Reasoning = "Detected custom intent"
            };
        }
        
        return baseSuggestion;
    }
}
```

## Streaming Events

### Emitting Custom Events

Emit custom streaming events for UI integration:

```csharp
var customEvent = StreamingEvent.Create(
    StreamingEventType.CommandSuggested,
    new CommandSuggestedPayload
    {
        CommandName = "mycommand",
        FormattedCommand = "/mycommand arg1",
        Confidence = 0.85,
        Reasoning = "User requested this action",
        ConfirmationRequestId = Guid.NewGuid().ToString()
    },
    correlationId);

await eventSink.EmitAsync(customEvent, ct);
```

### Handling Command Confirmation

Listen for confirmation events:

```csharp
if (event.Type == StreamingEventType.SuggestedCommandConfirmed)
{
    var payload = (SuggestedCommandConfirmedPayload)event.Payload;
    // Execute the confirmed command
    await ExecuteCommandAsync(payload.FormattedCommand, ct);
}
```

## UI Integration

### Registering Custom Event Renderers

Add custom event renderers for new event types:

```javascript
class CustomEventRenderer extends EventRendererBase {
    constructor() {
        super({
            eventType: 'custom.event',
            name: 'CustomEventRenderer',
            description: 'Renders custom events',
            version: '1.0.0',
            priority: 100
        });
    }

    render(event, context) {
        const html = `<div class="custom-event">${event.payload.message}</div>`;
        return this.createRenderResult(html, {
            elementId: this.generateElementId(event.id, 'custom'),
            append: true
        });
    }

    update(event, element, context) {
        return false;
    }
}

// Register
eventRendererRegistry.register(new CustomEventRenderer());
```

### Adding UI Controls

Extend the chat interface with custom controls:

```javascript
class CustomChatControl {
    constructor(chatElement) {
        this.chatElement = chatElement;
        this._initializeControls();
    }

    _initializeControls() {
        const control = document.createElement('button');
        control.className = 'custom-control';
        control.textContent = 'Custom Action';
        control.addEventListener('click', () => this._handleClick());
        this.chatElement.appendChild(control);
    }

    _handleClick() {
        // Handle custom action
    }
}
```

## Testing

### Unit Tests for Commands

```csharp
[Fact]
public async Task MyCommand_WithValidArgs_Succeeds()
{
    // Arrange
    var command = new MyNewCommand();
    var args = new[] { "arg1", "arg2" };
    
    // Act
    var result = await command.ExecuteAsync(args, CancellationToken.None);
    
    // Assert
    Assert.True(result.Success);
}
```

### Integration Tests for Chat

```csharp
[Fact]
public async Task ChatResponder_WithCommandKeywords_SuggestsCommand()
{
    // Arrange
    var responder = new LlmChatResponder(_llmProvider, _contextAssembly);
    var input = "Can you run the tests?";
    
    // Act
    var response = await responder.RespondAsync(input, CancellationToken.None);
    
    // Assert
    Assert.NotNull(response.SuggestedCommand);
    Assert.Equal("run", response.SuggestedCommand.CommandName);
}
```

## Performance Considerations

### Token Budget Management

Monitor and optimize token usage:

```csharp
var historyManager = new ConversationHistoryManager(maxTokenBudget: 4000);

// Check remaining budget before adding
if (historyManager.WouldExceedBudget(estimatedTokens))
{
    // Handle budget exceeded
    logger.LogWarning("Conversation history exceeding token budget");
}

// Add turn
historyManager.AddTurn("user", content, estimatedTokens);
```

### Caching Context

Cache expensive context assembly:

```csharp
public class CachedChatContextAssembly : IChatContextAssembly
{
    private ChatContext? _cachedContext;
    private DateTimeOffset _cacheTime;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    public async Task<ChatContext> AssembleAsync(WorkflowIntent intent, CancellationToken ct)
    {
        if (_cachedContext != null && DateTimeOffset.UtcNow - _cacheTime < _cacheDuration)
        {
            return _cachedContext;
        }

        _cachedContext = await _assembleContextAsync(intent, ct);
        _cacheTime = DateTimeOffset.UtcNow;
        return _cachedContext;
    }
}
```

## Troubleshooting

### Command Not Appearing in Help
- Verify command is registered in `CommandRegistry`
- Check that `Name` and `Syntax` are set correctly
- Ensure command is added to DI container

### Chat Responder Not Suggesting Commands
- Check `CommandSuggestionDetector` keywords
- Verify confidence threshold is met
- Review LLM provider configuration

### Streaming Events Not Rendering
- Verify event type is registered in renderer registry
- Check event payload structure matches expected type
- Review browser console for JavaScript errors

## References

- Command Interface: `nirmata.Agents/Execution/ControlPlane/Commands/ICommand.cs`
- Chat Responder: `nirmata.Agents/Execution/ControlPlane/Chat/IChatResponder.cs`
- Streaming Events: `nirmata.Web/Models/Streaming/StreamingEventType.cs`
- Event Renderers: `nirmata.Web/wwwroot/js/ievent-renderer.js`
