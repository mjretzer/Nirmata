## MODIFIED Requirements

### Requirement: Workspace validation requires baseline state artifacts
Workspace validation MUST enforce that the baseline state artifacts exist:
- `.aos/state/state.json` MUST exist and MUST be valid JSON
- `.aos/state/events.ndjson` MUST exist

If `.aos/state/events.ndjson` contains any non-empty lines:
- the file MUST be newline-delimited JSON (NDJSON)
- each non-empty line MUST be valid JSON
- each non-empty line MUST be a single JSON object (one object per line)
- each non-empty line MUST validate against the local event schema (`gmsd:aos:schema:event:v1`)

#### Scenario: Malformed state.json fails validation
- **GIVEN** `.aos/state/state.json` exists but is malformed JSON
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the malformed JSON artifact

#### Scenario: Malformed NDJSON event line fails validation
- **GIVEN** `.aos/state/events.ndjson` contains a non-empty line that is not valid JSON
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports the invalid event log line

#### Scenario: Non-object NDJSON event line fails validation
- **GIVEN** `.aos/state/events.ndjson` contains a non-empty line that parses as JSON but is not an object
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports that the event line must be a JSON object

#### Scenario: Event schema violation fails validation
- **GIVEN** `.aos/state/events.ndjson` contains a non-empty line with `schemaVersion` that violates `gmsd:aos:schema:event:v1`
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails with a non-zero exit code and reports a schema validation issue for `.aos/state/events.ndjson`

