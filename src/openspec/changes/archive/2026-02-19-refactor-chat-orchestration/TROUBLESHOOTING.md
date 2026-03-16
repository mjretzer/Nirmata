# Troubleshooting Guide: Chat and Command Interface

## Common Issues and Solutions

### Chat Not Responding

**Symptom:** User types a message but no response appears

**Possible Causes:**
1. LLM provider is unavailable
2. Network connectivity issue
3. Token budget exceeded
4. Chat responder not properly configured

**Solutions:**
1. Check LLM provider status
   ```bash
   curl http://localhost:5000/health/llm
   ```

2. Verify network connectivity
   - Check browser console for network errors
   - Verify API endpoint is accessible

3. Check token budget
   - Review conversation history length
   - Clear old conversations if needed
   - Increase token budget in configuration

4. Verify chat responder configuration
   ```csharp
   // Check DI container
   var responder = serviceProvider.GetRequiredService<IChatResponder>();
   Assert.NotNull(responder);
   ```

### Command Suggestions Not Appearing

**Symptom:** Chat about commands but no suggestions appear

**Possible Causes:**
1. Command suggestion detection disabled
2. Confidence threshold too high
3. Keywords not matching user input
4. LLM provider issues

**Solutions:**
1. Enable command suggestions
   ```csharp
   // In Program.cs
   services.AddSingleton<CommandSuggestionDetector>();
   ```

2. Lower confidence threshold
   ```csharp
   // In CommandSuggestionDetector
   if (confidence > 0.5) // Lower from 0.7
   {
       // Suggest command
   }
   ```

3. Check detection keywords
   ```csharp
   // Review CommandSuggestionKeywords in CommandSuggestionDetector
   private static readonly string[] CommandSuggestionKeywords = new[]
   {
       "run", "execute", "start", // Add more keywords
   };
   ```

4. Verify LLM provider
   - Check provider logs
   - Test with simple prompts
   - Verify API keys are correct

### Streaming Events Not Rendering

**Symptom:** Events stream but don't display in UI

**Possible Causes:**
1. Event renderer not registered
2. Event type mismatch
3. JavaScript error in renderer
4. CSS not loaded

**Solutions:**
1. Verify renderer registration
   ```javascript
   // In event-renderer-registry.js
   const renderer = new CommandProposalRenderer();
   registry.register(renderer);
   ```

2. Check event type matches
   ```javascript
   // Verify event.type matches renderer eventType
   console.log('Event type:', event.type);
   console.log('Renderer handles:', renderer.eventType);
   ```

3. Check browser console for errors
   - Open DevTools (F12)
   - Check Console tab for JavaScript errors
   - Check Network tab for failed requests

4. Verify CSS is loaded
   ```html
   <!-- In layout.html -->
   <link rel="stylesheet" href="/css/command-proposal-cards.css">
   <link rel="stylesheet" href="/css/command-autocomplete.css">
   ```

### Command Autocomplete Not Working

**Symptom:** Typing `/` doesn't show autocomplete suggestions

**Possible Causes:**
1. Autocomplete not initialized
2. Commands not registered
3. JavaScript error
4. CSS hiding dropdown

**Solutions:**
1. Initialize autocomplete
   ```javascript
   // In chat-input.js
   const autocomplete = new CommandAutocomplete(inputElement);
   autocomplete.setCommands(availableCommands);
   ```

2. Verify commands are registered
   ```javascript
   // Check available commands
   console.log('Available commands:', availableCommands);
   ```

3. Check browser console
   - Look for JavaScript errors
   - Verify autocomplete is instantiated
   - Check event listeners are attached

4. Verify CSS visibility
   ```css
   /* Ensure dropdown is visible */
   .command-autocomplete-dropdown {
       display: block; /* Not hidden */
       z-index: 1000; /* Above other elements */
   }
   ```

### Suggestion Accept/Reject Not Working

**Symptom:** Clicking Accept/Reject buttons has no effect

**Possible Causes:**
1. Event handlers not attached
2. API endpoint not responding
3. Confirmation ID not passed correctly
4. JavaScript error

**Solutions:**
1. Verify event handlers
   ```javascript
   // In command-proposal-renderers.js
   const acceptBtn = element.querySelector('.command-proposal-card__btn--accept');
   acceptBtn.addEventListener('click', () => {
       // Handle acceptance
   });
   ```

2. Check API endpoint
   ```bash
   curl -X POST http://localhost:5000/api/suggestion/confirm \
     -H "Content-Type: application/json" \
     -d '{"confirmationRequestId":"test-id"}'
   ```

3. Verify confirmation ID
   ```javascript
   // Check data attribute
   console.log('Confirmation ID:', element.dataset.confirmationId);
   ```

4. Check browser console for errors
   - Look for fetch errors
   - Check network requests
   - Verify response status

### Token Budget Exceeded

**Symptom:** Conversation stops responding after many messages

**Possible Causes:**
1. Conversation history too long
2. Token budget too low
3. Context assembly includes too much data
4. LLM response tokens not counted

**Solutions:**
1. Check conversation history
   ```csharp
   // In ConversationHistoryManager
   var tokenCount = historyManager.GetCurrentTokenCount();
   var budget = historyManager.GetRemainingTokenBudget();
   ```

2. Increase token budget
   ```csharp
   // In Program.cs
   services.AddSingleton(new ConversationHistoryManager(
       maxTokenBudget: 8000, // Increase from 4000
       minContextTurns: 3
   ));
   ```

3. Reduce context size
   ```csharp
   // In ChatContextAssembly
   // Include only essential workspace facts
   // Limit project description length
   // Reduce roadmap detail
   ```

4. Monitor token usage
   ```csharp
   // Log token counts
   logger.LogInformation("Token usage: {Used}/{Budget}",
       historyManager.GetCurrentTokenCount(),
       historyManager.GetRemainingTokenBudget());
   ```

### Intent Classification Wrong

**Symptom:** Chat messages classified as commands or vice versa

**Possible Causes:**
1. Classification model not trained well
2. Input ambiguous
3. Threshold settings wrong
4. Context not considered

**Solutions:**
1. Review classification logic
   ```csharp
   // In InputClassifier
   var classification = classifier.Classify(input);
   logger.LogInformation("Classification: {Intent} ({Confidence})",
       classification.Intent.Kind,
       classification.Confidence);
   ```

2. Adjust thresholds
   ```csharp
   // In InputClassifier
   if (confidence > 0.7) // Adjust threshold
   {
       // Classify as command
   }
   ```

3. Check for slash prefix
   ```csharp
   // Explicit slash should always be command
   if (input.StartsWith("/"))
   {
       return new Classification { Intent = IntentKind.Command };
   }
   ```

4. Review training data
   - Add more examples
   - Improve feature extraction
   - Consider context in classification

### Performance Issues

**Symptom:** Slow response times or UI lag

**Possible Causes:**
1. LLM provider slow
2. Context assembly expensive
3. Too many streaming events
4. Browser rendering issues

**Solutions:**
1. Profile LLM provider
   ```csharp
   // Measure response time
   var stopwatch = Stopwatch.StartNew();
   var response = await llmProvider.CompleteAsync(request, ct);
   stopwatch.Stop();
   logger.LogInformation("LLM response time: {Ms}ms", stopwatch.ElapsedMilliseconds);
   ```

2. Optimize context assembly
   ```csharp
   // Cache expensive operations
   // Reduce context size
   // Parallelize assembly
   ```

3. Throttle streaming events
   ```csharp
   // Batch events or reduce frequency
   // Only emit important events
   // Compress event payloads
   ```

4. Optimize browser rendering
   ```javascript
   // Use requestAnimationFrame for updates
   // Debounce event handlers
   // Virtualize long lists
   ```

## Debugging Tools

### Enable Debug Logging

```csharp
// In Program.cs
builder.Services.AddLogging(config =>
{
    config.SetMinimumLevel(LogLevel.Debug);
    config.AddConsole();
});
```

### Browser DevTools

1. **Console Tab**
   - Check for JavaScript errors
   - Log event details
   - Monitor network requests

2. **Network Tab**
   - Check API response times
   - Verify event stream
   - Monitor payload sizes

3. **Performance Tab**
   - Profile rendering
   - Identify bottlenecks
   - Monitor memory usage

### Test Endpoints

```bash
# Test chat endpoint
curl -X POST http://localhost:5000/api/chat/stream-v2 \
  -d "command=hello"

# Test suggestion confirmation
curl -X POST http://localhost:5000/api/suggestion/confirm \
  -H "Content-Type: application/json" \
  -d '{"confirmationRequestId":"test-id"}'

# Test health check
curl http://localhost:5000/health
```

## Getting Help

1. **Check Logs**
   - Application logs
   - Browser console
   - Network requests

2. **Review Documentation**
   - User Guide
   - Developer Guide
   - Implementation Notes

3. **Test in Isolation**
   - Test individual components
   - Use unit tests
   - Create minimal reproduction

4. **Contact Support**
   - Provide error messages
   - Include logs
   - Describe steps to reproduce

## Performance Baselines

Expected performance metrics:

- Chat response time: 1-3 seconds
- Command suggestion detection: <100ms
- Streaming event latency: <50ms
- UI rendering: 60 FPS
- Token budget usage: <80% of max

If your metrics are significantly worse, investigate the solutions above.
