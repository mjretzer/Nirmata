## ADDED Requirements
### Requirement: AOS config layer exists and can be validated
The system SHALL support a config layer rooted at `.aos/config/**`.

The config layer MUST support a primary config document at:
`.aos/config/config.json`.

The config document MUST be valid JSON and MUST be written using the canonical deterministic JSON writer defined by `aos-deterministic-json-serialization`.

The system SHALL provide a CLI command `aos config validate` that validates `.aos/config/config.json` and fails with an actionable error if invalid.

#### Scenario: Config validation succeeds
- **GIVEN** `.aos/config/config.json` exists and is valid per the local schema pack
- **WHEN** `aos config validate` is executed
- **THEN** the command succeeds with exit code 0

### Requirement: Secrets in config are references only
Config documents MUST represent secrets by reference only (e.g., environment variable references) and MUST NOT embed secret values directly.

The schema for `.aos/config/config.json` MUST model secret fields as a `SecretRef` object (not a raw string).

#### Scenario: Plaintext secrets are rejected
- **GIVEN** `.aos/config/config.json` contains a plaintext secret value (e.g., an API key as a JSON string)
- **WHEN** `aos config validate` is executed
- **THEN** the command fails with a stable non-zero exit code and an actionable error indicating that secrets must be references

