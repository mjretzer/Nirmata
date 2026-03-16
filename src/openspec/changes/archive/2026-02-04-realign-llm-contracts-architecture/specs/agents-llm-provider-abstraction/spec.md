## RENAMED Requirements

### Requirement: Capability renamed from aos-llm-provider-abstraction to agents-llm-provider-abstraction
- **FROM**: `aos-llm-provider-abstraction` (located in nirmata.Aos)
- **TO**: `agents-llm-provider-abstraction` (located in nirmata.Agents)

**Reason**: LLM provider abstractions are orchestration concerns owned by the Agent plane, not the AOS engine. The AOS engine provides workspace services (validation, state, evidence) but does not orchestrate LLM calls. Workflows in nirmata.Agents are the orchestrators that call LLMs.

**Migration**: 
- Update all imports from `nirmata.Aos.Contracts.Llm` to `nirmata.Agents.Contracts.Llm`
- Update all imports from `nirmata.Aos.Adapters` to `nirmata.Agents.Adapters`
- Update package references to include nirmata.Agents instead of/in addition to nirmata.Aos for LLM functionality

## MODIFIED Requirements

### Requirement: LLM calls are recorded as auditable evidence
The system SHALL record all LLM provider invocations using the existing call envelope infrastructure without nirmata.Aos having a compile-time dependency on nirmata.Agents.

**Migration notes:**
- **Previous location**: nirmata.Aos/Engine/Evidence/LlmCallEnvelope.cs
- **New approach**: LlmCallEnvelope remains in nirmata.Aos but operates on primitive types

Each LLM call MUST produce an `LlmCallEnvelope` record containing:
- `schemaVersion: 1`
- `runId` — correlation ID from execution context
- `callId` — unique identifier for this call
- `provider` — provider identifier as **string** (e.g., `openai`, `anthropic`), NOT `ILlmProvider.ProviderId` property access
- `model` — model identifier used as **string** (e.g., `gpt-4`, `claude-3-sonnet`)
- `status` — `succeeded` or `failed`
- `requestSummary` — truncated/summary of request (messages count, tools count)
- `responseSummary` — truncated/summary of response (finish reason, usage)
- `timestampUtc` — ISO 8601 timestamp
- `durationMs` — elapsed milliseconds

#### Scenario: Evidence capture without AOS→Agents dependency
- **GIVEN** a workflow in nirmata.Agents calls an LLM provider
- **WHEN** the Agents layer records evidence via IAosEvidenceWriter
- **THEN** nirmata.Aos captures the envelope using only string/primitive types
- **AND** nirmata.Aos does not reference nirmata.Agents.Contracts.Llm types

#### Scenario: Agent workflow records LLM call
- **GIVEN** a workflow in nirmata.Agents executes an LLM completion via ILlmProvider
- **WHEN** the workflow calls IAosEvidenceWriter to record the interaction
- **THEN** the envelope is written to `.aos/evidence/runs/<run-id>/logs/llm-<call-id>.json` with all metadata as strings
