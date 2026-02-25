# Proposal: Enforce Structured Outputs for Critical Planning Artifacts

## Problem
The agent engine currently relies on loosely structured or model-generated text that is later parsed or manually converted into JSON. This leads to:
- "Reasoning gates aren't existent" feelings because logic is hidden in text blocks.
- Brittle parsing in `PhasePlanner`, `FixPlanner`, and `CommandSuggestion`.
- Data contract mismatches between different engine components.

## Proposed Changes
Enforce strict JSON schema conformance for the following artifacts:
1. **Phase Planning**: Structured output defining tasks, file scopes, and verification steps.
2. **Fix Planning**: Structured output mapping issues to proposed diffs and tests.
3. **Command Proposal**: Structured output for the agent's "next action" intention.

## Goals
- Eliminate parsing errors for planning artifacts.
- Make agent reasoning structured, validated, and visible.
- Ensure 100% schema compliance for engine-executed JSON.

## Scope
- `Gmsd.Agents` logic for phase, fix, and command planning.
- `Gmsd.Aos` contracts and schemas for these artifacts.
- Prompts and LLM provider configurations to support structured outputs.
