# Design: Structured Planning Outputs

## Architecture

### 1. Schema Definition
We will define strict JSON schemas in `Gmsd.Aos` for:
- `PhasePlan`: `tasks[] { id, title, description, fileScopes[], verificationSteps[] }`
- `FixPlan`: `fixes[] { issueId, description, proposedChanges[] { file, description }, tests[] }`
- `CommandProposal`: `intent { command, group, rationale, expectedOutcome }`

### 2. LLM Provider Integration
Modify `ILlmProvider` or its implementations in `Gmsd.Agents` to support "Structured Output" or "Strict Mode" for tool schemas/response formats.

### 3. Orchestration Handlers
Update `PhasePlannerHandler`, `FixPlannerHandler`, and `LlmCommandSuggester` to:
1. Request structured JSON from the model.
2. Validate the output against the schema immediately upon receipt.
3. Persist the validated artifacts to `.aos/spec/` or `.aos/evidence/`.

## Data Flow
1. **Request**: Orchestrator sends prompt + schema to LLM.
2. **Response**: LLM returns strict JSON.
3. **Validation**: Engine validates JSON; if invalid, triggers retry or failure (though strict mode should prevent this).
4. **Execution**: Engine consumes validated JSON directly.

## Trade-offs
- **Model Support**: Requires models that support structured outputs (e.g., GPT-4o, Claude 3.5 Sonnet with tool use).
- **Flexibility**: Less "creative" freedom for the model in these specific artifacts, which is intentional for reliability.
