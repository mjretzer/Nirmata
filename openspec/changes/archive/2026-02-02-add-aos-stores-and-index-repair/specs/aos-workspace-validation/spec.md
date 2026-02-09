## ADDED Requirements
### Requirement: Workspace validation requires baseline spec catalog indexes
Workspace validation MUST enforce that required spec catalog indexes exist and are valid JSON:
- `.aos/spec/milestones/index.json`
- `.aos/spec/phases/index.json`
- `.aos/spec/tasks/index.json`
- `.aos/spec/issues/index.json`
- `.aos/spec/uat/index.json`

#### Scenario: Missing required spec catalog index fails validation
- **GIVEN** `.aos/spec/issues/index.json` is missing
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the missing artifact

### Requirement: Workspace validation requires baseline state artifacts
Workspace validation MUST enforce that the baseline state artifacts exist:
- `.aos/state/state.json` MUST exist and MUST be valid JSON
- `.aos/state/events.ndjson` MUST exist

If `.aos/state/events.ndjson` contains any non-empty lines, each non-empty line MUST be valid JSON.

#### Scenario: Malformed state.json fails validation
- **GIVEN** `.aos/state/state.json` exists but is malformed JSON
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the malformed JSON artifact

#### Scenario: Malformed NDJSON event line fails validation
- **GIVEN** `.aos/state/events.ndjson` contains a non-empty line that is not valid JSON
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the invalid event log line

### Requirement: Workspace validation requires baseline evidence artifacts
Workspace validation MUST enforce that baseline evidence artifacts exist and are valid JSON:
- `.aos/evidence/logs/commands.json`
- `.aos/evidence/runs/index.json`

#### Scenario: Missing evidence command log fails validation
- **GIVEN** `.aos/evidence/logs/commands.json` is missing
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the missing artifact

