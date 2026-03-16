## ADDED Requirements

### Requirement: Workspace validation validates context pack artifacts when present
When `.aos/context/packs/**` contains context pack artifacts, `aos validate workspace` MUST validate each canonical pack file against the local schema pack using schema id `nirmata:aos:schema:context-pack:v1`.

Context pack files are canonical when named `PCK-####.json` and located under `.aos/context/packs/`.

Workspace validation MUST report schema failures using the normalized schema issue form (contract path, schema id, instance location, message).

#### Scenario: Present context pack is schema-validated
- **GIVEN** `.aos/context/packs/PCK-0001.json` exists
- **WHEN** `aos validate workspace` is executed
- **THEN** the command fails if the pack is invalid JSON or violates the context pack schema

#### Scenario: Context pack schema violations report instance location
- **GIVEN** `.aos/context/packs/PCK-0001.json` exists and violates a required property in the context pack schema
- **WHEN** `aos validate workspace` is executed
- **THEN** the validation report includes the context pack schema id and the invalid property location

