# aos-state-transition-engine Specification

## Purpose
TBD - created by archiving change add-aos-control-plane-primitives. Update Purpose after archive.
## Requirements
### Requirement: State transitions are validated and atomic
The system SHALL provide a state transition engine that validates requested transitions against the current state snapshot and rejects invalid transitions.

When a transition is rejected, the system MUST NOT partially write state artifacts (no partial/corrupt `state.json`, and no “half-recorded” transitions).

#### Scenario: Invalid transition is rejected without mutating state
- **GIVEN** a workspace with an existing `.aos/state/state.json`
- **WHEN** an invalid transition is requested (one that is not allowed by the transition rules)
- **THEN** the operation fails with a stable non-zero exit code and `.aos/state/state.json` remains unchanged

