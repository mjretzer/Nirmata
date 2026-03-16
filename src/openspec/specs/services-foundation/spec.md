# services-foundation Specification

## Purpose

Defines product service-layer conventions and behavior for $capabilityId.

- **Lives in:** `nirmata.Services/*`
- **Owns:** Application service boundaries and business-logic conventions
- **Does not own:** Engine workflows or persistence implementation details
## Requirements
### Requirement: Service Interface Conventions
The services layer SHALL define interfaces for service contracts and keep all implementation details internal. Controllers SHALL depend only on interfaces, not concrete implementations.

#### Scenario: Controller usage
- **WHEN** a controller performs a business operation
- **THEN** it calls a service interface method
- **AND** the service returns DTOs, not entities

### Requirement: Transaction Boundaries
The services layer SHALL own transaction boundaries and call `SaveChangesAsync` within service methods. Controllers SHALL not directly call `SaveChangesAsync`.

#### Scenario: Create workflow
- **WHEN** the thin-slice entity is created
- **THEN** the service persists changes atomically within one `SaveChangesAsync` call

### Requirement: Mapping Policy
The services layer SHALL use centralized AutoMapper profiles for entity-to-DTO mapping. Service methods SHALL return DTOs, not entities, to decouple the API from the data model.

#### Scenario: Entity to DTO mapping
- **WHEN** a service returns a result
- **THEN** it maps the entity to a DTO using AutoMapper before returning
- **AND** the controller receives only DTO objects

### Requirement: Thin-Slice Service via AutoMapper profiles
The services layer SHALL implement a minimal Project service using AutoMapper profiles for all entity-to-DTO transformations.

#### Scenario: Create and read with DTOs
- **WHEN** a Project create request is processed
- **THEN** the service maps the request DTO to an entity, stores the Project, and returns a response DTO
- **AND** retrieval operations return DTOs instead of entities

### Requirement: Services Composition Root
The system SHALL provide a centralized extension method `AddnirmataServices()` to register all service dependencies in the DI container.

#### Scenario: API composition
- **WHEN** the API application starts
- **THEN** it calls `builder.Services.AddnirmataServices()` to register all service interfaces and implementations
- **AND** no service registration happens outside this composition root

### Requirement: Service-Level Testing
The system SHALL provide unit tests for service implementations that verify business logic, mapping, and transaction behavior.

#### Scenario: Service test coverage
- **WHEN** a service implementation is modified
- **THEN** existing tests verify the change does not break expected behavior
- **AND** tests use in-memory database or mocks to isolate the service layer

