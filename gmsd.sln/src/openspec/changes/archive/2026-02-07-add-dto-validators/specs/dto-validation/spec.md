## ADDED Requirements

### Requirement: Request Validators
The system SHALL provide dedicated validator classes for request DTOs, located in `Gmsd.Data.Dto/Validators/` with a folder structure mirroring `Requests/`.

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
The `Gmsd.Data.Dto` project SHALL organize validators under `Validators/[Category]/` matching the `Requests/[Category]/` structure.

#### Scenario: File placement
- **WHEN** a new request validator is added
- **THEN** it is placed in the corresponding `Validators/` subfolder matching its request DTO location

### Requirement: Unit Testable Validators
Validators SHALL be unit testable without requiring ASP.NET Core pipeline setup.

#### Scenario: Direct validator invocation
- **WHEN** a validator is instantiated directly in a unit test
- **THEN** it can validate a DTO and return validation results independently of the HTTP pipeline

## MODIFIED Requirements

### Requirement: DataAnnotations Validation
Request DTOs SHALL use DataAnnotations for validation and integrate with ASP.NET Core model validation.

#### Scenario: Required field missing
- **WHEN** a required field is omitted from a request
- **THEN** the API returns a 400 `ProblemDetails` with field-level validation errors

#### Scenario: Validator composition
- **GIVEN** a request DTO has DataAnnotations attributes
- **WHEN** a matching FluentValidation validator exists
- **THEN** both validation layers execute and aggregate errors
