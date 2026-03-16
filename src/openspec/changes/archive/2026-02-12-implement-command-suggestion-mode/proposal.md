# Change: Implement Command Suggestion Mode

## Why

The strict command grammar (`/run`, `/plan`, `/status`) successfully prevents accidental workflow triggers from freeform chat. However, this creates a discoverability problem: users must know the exact command syntax before they can accomplish anything.

The system should bridge the gap between natural language and explicit commands by offering a "suggestion mode" where:
- Freeform input that looks workflow-related is recognized
- The agent proposes the explicit command with properly formatted arguments
- The user confirms before any write operation executes

This preserves the safety of the strict command grammar while improving UX through conversational assistance.

## What Changes

- Add `ICommandSuggester` service to analyze freeform input and propose explicit commands
- Integrate suggestion mode into `InputClassifier` as an optional second-stage path
- Add structured output contract for command proposals (command name + arguments + reasoning)
- Emit `command.suggested` streaming event before confirmation request
- Update confirmation gate to handle suggested commands with pre-filled arguments
- Add LLM prompt template for command suggestion with examples

## Impact

- **Affected specs**: `intent-classification` (new requirement implementation)
- **Affected code**: 
  - `nirmata.Agents/Execution/Preflight/InputClassifier.cs` (add suggestion mode hook)
  - `nirmata.Agents/Execution/Preflight/CommandSuggestion/...` (new)
  - `nirmata.Agents/Execution/Preflight/ConfirmationGate.cs` (handle suggested commands)
- **UI impact**: Chat UI will receive `command.suggested` events before confirmation prompts
- **New dependency**: LLM integration for natural language understanding

## Relationship to Existing Work

- Builds on `fix-intent-classification-false-positives` (strict command grammar already implemented)
- Implements spec requirement 5 from `intent-classification/spec.md`
- Aligns with Phase 2 in Remediation.md roadmap: "Add 'command suggestion' mode for freeform"
