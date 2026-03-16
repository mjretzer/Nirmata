# Engine Schema Registry Service

## ADDED Requirements

### Requirement: Schema registry interface exists
The system SHALL define `ISchemaRegistry` as a public interface in `nirmata.Aos/Public/`.

The interface SHALL provide methods to load and retrieve JSON schemas by their `$id`.

#### Scenario: Load schema by $id
- **GIVEN** a valid schema `$id` (e.g., `nirmata:aos:schema:project:v1`)
- **WHEN** `ISchemaRegistry.GetSchema(schemaId)` is called
- **THEN** the corresponding JSON schema is returned

#### Scenario: Missing schema $id fails fast
- **GIVEN** an invalid or unknown schema `$id`
- **WHEN** `ISchemaRegistry.GetSchema(schemaId)` is called
- **THEN** a deterministic, actionable exception is thrown

#### Scenario: List all available schema IDs
- **WHEN** `ISchemaRegistry.ListSchemaIds()` is called
- **THEN** all available schema `$id` values are returned

### Requirement: Embedded schemas are supported
The interface SHALL provide access to schemas embedded in the `nirmata.Aos` assembly.

#### Scenario: Load embedded project schema
- **GIVEN** the embedded project schema with `$id` `nirmata:aos:schema:project:v1`
- **WHEN** `ISchemaRegistry.GetSchema("nirmata:aos:schema:project:v1")` is called
- **THEN** the embedded project schema is returned

#### Scenario: Duplicate embedded $id fails fast at startup
- **GIVEN** two embedded schema files with the same `$id`
- **WHEN** the `ISchemaRegistry` is initialized
- **THEN** an exception is thrown identifying the conflicting schemas

#### Scenario: Missing $id in embedded schema fails fast
- **GIVEN** an embedded schema file without a `$id` property
- **WHEN** the `ISchemaRegistry` is initialized
- **THEN** an exception is thrown identifying the schema file

### Requirement: Local schema pack is supported
The interface SHALL support loading schemas from the local schema pack under `.aos/schemas/`.

#### Scenario: Load local schema from registry
- **GIVEN** a local schema pack with `registry.json` and schema files
- **WHEN** `ISchemaRegistry.GetSchema(schemaId)` is called for a local schema
- **THEN** the local schema is returned

#### Scenario: Local schema overrides embedded when same $id
- **GIVEN** a local schema with the same `$id` as an embedded schema
- **WHEN** `ISchemaRegistry.GetSchema(schemaId)` is called
- **THEN** the local schema is returned (local takes precedence)

### Requirement: Service is registered in DI
The system SHALL register `ISchemaRegistry` as a Singleton in `AddnirmataAos()`.

#### Scenario: Plane resolves the service via DI
- **GIVEN** a configured service collection with `AddnirmataAos()` called
- **WHEN** `serviceProvider.GetRequiredService<ISchemaRegistry>()` is called
- **THEN** a non-null implementation is returned

## Cross-References
- `aos-schema-registry` - Defines full schema registry requirements
