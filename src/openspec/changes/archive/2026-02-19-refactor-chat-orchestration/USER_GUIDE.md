# User Guide: Unified Chat and Command Interface

## Overview

nirmata Assistant now provides a unified conversational interface that seamlessly handles both natural chat and explicit commands. This guide explains how to use the new chat-first approach.

## Chat vs Commands

### Chat (Default)
Simply type your message without a slash prefix to chat naturally with the assistant:

```
You: What's the current status of the project?
Assistant: [Provides conversational response with workspace context]
```

**Features:**
- Natural, conversational interaction
- Workspace-aware responses
- Read-only access (no side effects)
- Can ask questions, get explanations, discuss ideas

### Commands (Explicit)
Use a slash prefix (`/`) to execute explicit commands:

```
You: /run test-suite
Assistant: [Executes the command and streams results]
```

**Available Commands:**
- `/help` - Show available commands
- `/status` - Get current project status
- `/run <workflow>` - Execute a workflow
- `/plan <task>` - Create a plan for a task
- `/verify <item>` - Verify a specification or artifact
- `/fix <issue>` - Attempt to fix an issue

## Command Suggestions

When you chat about wanting to execute something, the assistant may suggest a command:

```
You: Can you run the test suite?
Assistant: [Displays command suggestion card]
  Suggested Command: /run test-suite
  Why: You asked to run the test suite
  [Accept] [Reject]
```

**What to do:**
- **Accept:** Click the Accept button to execute the suggested command
- **Reject:** Click the Reject button to dismiss the suggestion and continue chatting

## Command Autocomplete

As you type a slash command, autocomplete suggestions appear:

```
You: /r[autocomplete dropdown appears]
  /run - Execute a workflow
  /repair - Repair a file or component
```

**Navigation:**
- Use **Arrow Up/Down** to navigate suggestions
- Press **Enter** to select
- Press **Escape** to close

## Examples

### Example 1: Chat About a Feature
```
You: How would I implement a new authentication system?
Assistant: Based on your current project structure, here's an approach...
[Provides detailed conversational response]
```

### Example 2: Chat Leading to Command Suggestion
```
You: I want to verify the API specification
Assistant: [Displays command suggestion]
  Suggested Command: /verify api-spec
  [Accept] [Reject]
```

### Example 3: Direct Command Execution
```
You: /run integration-tests
Assistant: [Streams test execution results]
```

## Tips and Best Practices

### 1. Use Chat for Questions and Discussion
- Ask about workspace state
- Get explanations and advice
- Discuss implementation approaches
- Get help understanding errors

### 2. Use Commands for Actions
- Execute workflows
- Run tests
- Create plans
- Verify specifications

### 3. Accept Suggestions When Confident
- The assistant suggests commands when it's confident about your intent
- Confidence scores are shown (e.g., "Confidence: 85%")
- Accept suggestions to quickly execute intended actions

### 4. Reject and Clarify if Uncertain
- If a suggestion doesn't match your intent, reject it
- Continue the conversation to clarify
- The assistant will provide alternative suggestions

## Troubleshooting

### "I don't see a command suggestion"
- The assistant only suggests commands when confident about your intent
- Use explicit slash commands if you want guaranteed execution
- Try being more specific in your chat message

### "The suggestion is wrong"
- Click Reject to dismiss it
- Rephrase your request more clearly
- Use explicit slash commands instead

### "Autocomplete isn't showing"
- Start typing a slash: `/`
- Wait a moment for suggestions to appear
- Type at least one character after the slash

### "Command failed"
- Check the error message for details
- Verify the command syntax is correct
- Use `/help` to see command usage

## Conversation History

Your conversation history is maintained within a session, allowing the assistant to:
- Remember previous context
- Reference earlier messages
- Maintain conversation continuity

**Note:** Very long conversations may be optimized to stay within token budgets. The most recent messages and important context are always preserved.

## Privacy and Safety

- Chat interactions are read-only by default
- Commands requiring write access will ask for confirmation
- All interactions are logged for audit purposes
- Sensitive information should not be shared in chat

## Getting Help

- Type `/help` to see available commands
- Ask the assistant questions about how to use it
- Refer to the developer guide for advanced usage
