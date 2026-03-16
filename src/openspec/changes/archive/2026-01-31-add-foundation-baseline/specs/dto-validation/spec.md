## ADDED Requirements
### Requirement: DTOs as API Contract
The API SHALL use request/response DTOs as the external contract and SHALL NOT expose domain entities.

#### Scenario: Controller response
- **WHEN** a controller returns data
- **THEN** it returns DTOs rather than entity types

### Requirement: DataAnnotations Validation
Request DTOs SHALL use DataAnnotations for validation and integrate with ASP.NET Core model validation.

#### Scenario: Required field missing
- **WHEN** a required field is omitted from a request
- **THEN** the API returns a 400 `ProblemDetails` with field-level validation errors

### Requirement: Centralized Mapping Definitions
DTO mapping definitions SHALL live in a centralized mapping profile.

#### Scenario: Mapping discovery
- **WHEN** a new DTO is added
- **THEN** its mapping is defined in the shared profile rather than scattered in controllers

### Requirement: Thin-Slice DTOs
The system SHALL provide request and response DTOs for the thin-slice endpoint.

#### Scenario: Thin-slice response
- **WHEN** the thin-slice endpoint returns a created entity
- **THEN** the response DTO includes the identifiers and user-visible fields

### Requirement: Project Create Request DTO
The system SHALL define a Project create request DTO with a required `Name` field and a maximum length of 200 characters.

#### Scenario: Create request validation
- **WHEN** a Project create request is missing `Name` or exceeds 200 characters
- **THEN** validation fails with field-level errors

### Requirement: Project Response DTO
The system SHALL define a Project response DTO containing `ProjectId` and `Name`.

#### Scenario: Project response shape
- **WHEN** a Project is returned from the API
- **THEN** the response DTO includes `ProjectId` and `Name`
