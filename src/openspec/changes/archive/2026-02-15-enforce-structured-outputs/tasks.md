# Tasks: Enforce Structured Outputs

## Phase 1: AOS Contracts & Schema (Foundation)
- [x] Define `PhasePlan` JSON schema in `nirmata.Aos.Contracts`.
- [x] Define `FixPlan` JSON schema in `nirmata.Aos.Contracts`.
- [x] Define `CommandProposal` JSON schema in `nirmata.Aos.Contracts`.
- [x] Add schema validation tests in `nirmata.Aos.Tests`.

## Phase 2: LLM Infrastructure
- [x] Update `ILlmProvider` to support structured output requirements.
- [x] Implement strict schema enforcement in the default LLM provider.

## Phase 3: Phase Planning Implementation
- [x] Update `PhasePlanner` to use structured output for plan generation.
- [x] Refactor `PhasePlannerHandler` to consume validated JSON.
- [x] Verify with integration tests.

## Phase 4: Fix Planning Implementation
- [x] Update `FixPlanner` to use structured output for issue-to-fix mapping.
  - [x] Integrate the validated `FixPlan` schema into plan generation and serialization.
  - [x] Add unit tests ensuring each issue-to-fix mapping conforms to the schema.
  - [x] Implement error handling for malformed or incomplete LLM responses before returning a plan.
- [x] Refactor `FixPlannerHandler` to consume validated JSON.
  - [x] Parse the structured payload into existing domain models using the shared schema utilities.
  - [x] Add guardrails/logging so invalid JSON or schema violations fail fast before dispatching commands.
  - [x] Update telemetry to capture schema validation failures and emitted fix plans.
- [x] Verify with integration tests.
  - [x] Extend the fix-planning integration suite with a happy-path structured-output scenario.
  - [x] Add a schema-mismatch test that exercises error propagation to the caller.
  - [x] Regress legacy text-flow behavior (if still supported) to ensure no unintended changes.

## Phase 5: Command Proposal Implementation
- [x] Update `LlmCommandSuggester` to use structured output for next-action proposals.
  - [x] Adopt the `CommandProposal` schema in prompt construction and response parsing.
  - [x] Validate generated proposals against the schema and surface detailed errors on mismatch.
  - [x] Add targeted unit tests covering multi-command and multi-argument responses.
- [x] Refactor `CommandParser` if necessary to handle structured intents.
  - [x] Map structured JSON intents into command objects, preserving backwards compatibility for legacy inputs.
  - [x] Harden validation so missing fields or extra commands are rejected with actionable messages.
  - [x] Update parser-focused telemetry/metrics to differentiate structured vs legacy parsing outcomes.
- [x] Verify with performance and integration tests.
  - [x] Run performance benchmarks to ensure structured parsing stays within latency budgets.
  - [x] Extend end-to-end command proposal tests to cover success, schema failure, and fallback cases.
  - [x] Document validation/perf findings in the change log once the suite is green.
