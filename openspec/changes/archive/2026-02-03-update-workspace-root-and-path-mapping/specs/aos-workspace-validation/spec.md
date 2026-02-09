## MODIFIED Requirements
### Requirement: Workspace validation CLI is available
The system SHALL provide a CLI command `aos validate workspace` that validates an AOS workspace rooted at `.aos/`.

`aos validate workspace` MUST default to validating all layers:
- `spec`
- `state`
- `evidence`
- `codebase`
- `context`
- `config`

#### Scenario: Workspace validation defaults to all layers
- **WHEN** `aos validate workspace` is executed without options
- **THEN** validation runs across all layers and reports any failures

### Requirement: Workspace validation supports explicit layer selection
`aos validate workspace` SHALL support an option `--layers` to validate an explicit subset of layers.

The `--layers` value MUST be a comma-separated list of layer names from:
`spec,state,evidence,codebase,context,config`.

#### Scenario: Workspace validation validates only selected layers
- **WHEN** `aos validate workspace --layers spec,state` is executed
- **THEN** only the `spec` and `state` layers are validated and other layers are not validated

## ADDED Requirements
### Requirement: Workspace validation validates config when present
Workspace validation MUST treat config as an optional layer: config validation MUST NOT fail solely because `.aos/config/config.json` is missing.

If `.aos/config/config.json` exists, workspace validation MUST validate it against the local schema pack.

#### Scenario: Missing config does not fail validation
- **GIVEN** a compliant workspace where `.aos/config/config.json` does not exist
- **WHEN** `aos validate workspace` is executed
- **THEN** validation does not report a failure solely due to missing config

#### Scenario: Present config is schema-validated
- **GIVEN** `.aos/config/config.json` exists
- **WHEN** `aos validate workspace` is executed
- **THEN** validation reports a failure if the config is invalid JSON or does not conform to the config schema

