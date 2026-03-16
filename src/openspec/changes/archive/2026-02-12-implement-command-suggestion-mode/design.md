## Design: Command Suggestion Mode

### Overview

The command suggestion mode adds a second-stage layer to the classifier that uses an LLM to analyze freeform chat input and determine if it represents an intent that could be better served by an explicit command.

### Key Design Decisions

1. **Opt-in Activation**: Suggestion mode is triggered when:
   - Input has no command prefix (freeform chat)
   - Input contains keywords that overlap with registered command descriptions
   - User preference or session setting enables suggestion mode

2. **LLM-Based Analysis**: Uses a lightweight LLM call with structured output to:
   - Extract potential command intent from natural language
   - Map arguments to command parameters
   - Provide confidence score and reasoning

3. **Structured Proposal Contract**: Command suggestions are typed objects:
   ```csharp
   public sealed class CommandProposal
   {
       public string CommandName { get; init; }      // e.g., "plan"
       public Dictionary<string, string> Arguments { get; init; }  // e.g., { "phase-id": "PH-0001" }
       public double Confidence { get; init; }         // 0.0 to 1.0
       public string Reasoning { get; init; }         // "User wants to create a plan for foundation phase"
       public string SuggestedInput { get; init; }    // "/plan --phase-id PH-0001"
   }
   ```

4. **No Auto-Execution**: Suggestions always require explicit user confirmation before the command is executed, even for read-only operations.

### Component Design

```
User Input (freeform)
    ↓
InputClassifier.Classify()
    ↓
No prefix detected → Chat intent
    ↓
SuggestionMode.IsEnabled && KeywordsMatch?
    ↓
Yes: ICommandSuggester.SuggestCommand(input)
    ↓
LLM Prompt: "Analyze this input and suggest a command if applicable"
    ↓
CommandProposal (or null if not applicable)
    ↓
Emit command.suggested event → UI displays proposal
    ↓
User confirms/denies
    ↓
Execute command or continue as chat
```

### LLM Prompt Strategy

**System Prompt**:
```
You are a command suggestion assistant. Analyze the user's natural language input and determine if it maps to one of the available commands.

Available commands:
- /run: Execute the current task or workflow
- /plan [options]: Create or update a plan
- /status: Check current workflow status
- /verify: Verify task execution
- /fix: Create a fix plan for issues
- /pause: Pause workflow execution
- /resume: Resume paused workflow
- /help: Show available commands

Respond with a structured proposal only if:
1. The input clearly indicates a command intent
2. You can map specific arguments from the input
3. Confidence is above 0.7

Otherwise, respond with "no_suggestion".
```

**Response Schema** (structured output):
```json
{
  "suggested": true,
  "command": "plan",
  "arguments": { "phase-id": "PH-0001" },
  "confidence": 0.85,
  "reasoning": "User wants to plan the foundation phase",
  "formatted": "/plan --phase-id PH-0001"
}
```

### Integration Points

1. **InputClassifier**: Add `EnableSuggestionMode` property and hook after default chat classification
2. **Streaming Events**: New event type `command.suggested` with proposal details
3. **ConfirmationGate**: Handle `CommandProposal` inputs (not just raw classification results)
4. **UI Contract**: Display suggestion card with "Execute" / "Dismiss" actions

### Error Handling

- LLM unavailable: Gracefully fall back to normal chat (no suggestion)
- Malformed proposal: Log and treat as chat
- Low confidence (< 0.7): Do not suggest, treat as chat
- Unknown command in proposal: Reject and suggest available commands

### Testing Strategy

1. Unit tests for `CommandSuggester` with mock LLM
2. Integration tests for full flow: freeform input → suggestion → confirmation
3. Edge cases: ambiguous input, multiple possible commands, invalid arguments
