# dto-validation Specification

## Purpose

Defines DTO and validation contracts for $capabilityId.

- **Lives in:** `nirmata.Data.Dto/*`
- **Owns:** External contract DTO shapes and validation conventions
- **Does not own:** Persistence entities, orchestration workflows, or engine contracts
## Requirements
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

#### Scenario: Validator composition
- **GIVEN** a request DTO has DataAnnotations attributes
- **WHEN** a matching FluentValidation validator exists
- **THEN** both validation layers execute and aggregate errors

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

### Requirement: Request Validators
The system SHALL provide dedicated validator classes for request DTOs, located in `nirmata.Data.Dto/Validators/` with a folder structure mirroring `Requests/`.

#### Scenario: Project create request validation
- **WHEN** a `ProjectCreateRequestDto` is submitted
- **THEN** the `ProjectCreateRequestValidator` validates the Name field is not empty and does not exceed 200 characters

#### Scenario: Validation failure response
- **WHEN** a request fails validation
- **THEN** the API returns 400 `ProblemDetails` with field-level error details

### Requirement: Validator Library Integration
The system SHALL integrate a validation library (e.g., FluentValidation) to provide composable, testable validation rules beyond DataAnnotations.

#### Scenario: Validator registration
- **WHEN** the API starts
- **THEN** all request validators are registered in the DI container and automatically invoked for incoming requests

### Requirement: Validator Project Structure
The `nirmata.Data.Dto` project SHALL organize validators under `Validators/[Category]/` matching the `Requests/[Category]/` structure.

#### Scenario: File placement
- **WHEN** a new request validator is added
- **THEN** it is placed in the corresponding `Validators/` subfolder matching its request DTO location

### Requirement: Unit Testable Validators
Validators SHALL be unit testable without requiring ASP.NET Core pipeline setup.

#### Scenario: Direct validator invocation
- **WHEN** a validator is instantiated directly in a unit test
- **THEN** it can validate a DTO and return validation results independently of the HTTP pipeline

