## ADDED Requirements
### Requirement: Service Interface Conventions
The services layer SHALL define interfaces for service contracts and keep controllers thin.

#### Scenario: Controller usage
- **WHEN** a controller performs a business operation
- **THEN** it calls the corresponding service interface

### Requirement: Transaction Boundaries
The services layer SHALL own transaction boundaries and call `SaveChanges` for atomic operations.

#### Scenario: Create workflow
- **WHEN** the thin-slice entity is created
- **THEN** the service persists changes atomically within one `SaveChanges` call

### Requirement: Mapping Policy
The services layer SHALL use centralized AutoMapper profiles for entity-to-DTO transformations.

#### Scenario: Entity to DTO mapping
- **WHEN** a service returns a DTO
- **THEN** the mapping is performed via AutoMapper profiles

### Requirement: Thin-Slice Service
The services layer SHALL implement a minimal Project service for creating and reading the Project thin-slice entity.

#### Scenario: Create and read
- **WHEN** a Project create request is processed
- **THEN** the service stores the Project and allows it to be retrieved by id
