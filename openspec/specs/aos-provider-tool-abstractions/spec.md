# aos-provider-tool-abstractions Specification

## Purpose
TBD - created by archiving change add-aos-control-plane-primitives. Update Purpose after archive.
## Requirements
### Requirement: Provider/tool calls are recorded with auditable envelopes
The system SHALL record external provider/tool calls (e.g., LLM calls, tool invocations) using an auditable call envelope format.

Call envelopes MUST be written under the current run evidence logs:
`.aos/evidence/runs/<run-id>/logs/**`.

Each envelope MUST include at least:
- `schemaVersion` (integer)
- `runId` (string)
- `callId` (string)
- `provider` (string)
- `tool` (string)
- `status` (string; e.g., `succeeded` or `failed`)

#### Scenario: A provider call records a call envelope
- **GIVEN** an orchestration run that invokes a provider/tool
- **WHEN** the invocation completes (success or failure)
- **THEN** a call envelope record exists under `.aos/evidence/runs/<run-id>/logs/**`

### Requirement: Replay can be implemented from recorded envelopes
The call envelope format SHALL include sufficient information to support a future replay mode where calls can be satisfied from previously recorded envelopes (where applicable).

#### Scenario: Recorded envelopes contain sufficient identifiers for matching
- **GIVEN** a run with one or more recorded call envelopes
- **WHEN** an operator inspects the envelopes
- **THEN** each envelope contains stable identifiers (provider/tool/callId) suitable for correlating a replayed call to a recorded call

