# Change: Add DTO Request Validators

## Why
The nirmata.Data.Dto project currently has DTO models in `Models/` and request shapes in `Requests/`, but lacks a dedicated `Validators/` folder for complex validation logic beyond DataAnnotations. The roadmap item PH-PRD-0002 requires completing the DTO layer with proper validators to ensure API request validation aligns with business constraints.

## What Changes
- Add `nirmata.Data.Dto/Validators/` folder structure mirroring the `Requests/` organization
- Create request validators for Project-related DTOs using a validation library (FluentValidation or similar)
- Wire validators into the ASP.NET Core validation pipeline
- Ensure validation failures produce consistent 400 `ProblemDetails` responses per `api-foundation` spec

## Impact
- **Affected specs:** `dto-validation`, `api-foundation`
- **Affected code:** `nirmata.Data.Dto/Validators/**`, `nirmata.Api` validation configuration
- **New dependencies:** Validation library (e.g., FluentValidation)
