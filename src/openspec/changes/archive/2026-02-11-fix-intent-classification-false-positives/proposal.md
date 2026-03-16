# Change: Fix Intent Classification False Positives

## Why

The orchestrator front door uses a classifier that decides whether an input has side effects (`None`, `ReadOnly`, `Write`). The current implementation treats many ordinary English requests as "workflow writes" instead of "conversation" because it matches common workflow verbs ("create", "plan", "run", "execute", "fix", etc.) anywhere in the input text.

This creates the "no matter what you say, it creates a run" experience - users cannot have freeform conversations without accidentally triggering write operations.

## What Changes

- **BREAKING**: Change default behavior from "keyword-based workflow detection" to "explicit command grammar"
- Introduce strict command prefix pattern (`/run`, `/plan`, `/status`, `/help`, etc.)
- Default freeform text (no prefix) to `SideEffect.None` / chat intent
- Add confirmation gate for write operations triggered by ambiguous classification
- Replace regex-based keyword matching with structured command parser
- Expose classification reasoning through streaming events for transparency

## Impact

- **Affected specs**: new capability `intent-classification`
- **Affected code**: `nirmata.Agents/Execution/Preflight/InputClassifier.cs`, `nirmata.Agents/Execution/ControlPlane/Orchestrator.cs`
- **UI impact**: ChatStreamingController event stream will include `intent.classified` events
- **Breaking change**: Existing keyword-based workflow triggers will no longer work; users must use explicit `/command` syntax or confirm via dialogue
