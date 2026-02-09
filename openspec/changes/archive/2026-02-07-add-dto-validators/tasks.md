## 1. Setup Validation Infrastructure
- [x] 1.1 Add FluentValidation (or chosen validation library) package to `Gmsd.Data.Dto.csproj`
- [x] 1.2 Add FluentValidation.AspNetCore package to `Gmsd.Api.csproj` for pipeline integration
- [x] 1.3 Create `Gmsd.Data.Dto/Validators/` folder structure matching `Requests/` hierarchy

## 2. Implement Project Validators
- [x] 2.1 Create `ProjectCreateRequestValidator` in `Validators/Projects/`
  - Validate Name is not empty and within 200 character limit
  - Validate no special character restrictions if applicable
- [x] 2.2 Ensure validator integrates with DataAnnotations rules (or replaces them consistently)

## 3. API Integration
- [x] 3.1 Register validators in `Gmsd.Api` DI container (`AddFluentValidationAutoValidation()`)
- [x] 3.2 Verify validation failures return 400 `ProblemDetails` with field-level errors
- [x] 3.3 Ensure validation runs before controller action execution

## 4. Testing & Verification
- [x] 4.1 Add unit tests for `ProjectCreateRequestValidator`
  - Valid request passes
  - Empty name fails
  - Name > 200 chars fails
- [x] 4.2 Run compile check: `dotnet build` passes
- [x] 4.3 Run API integration test: POST invalid DTO returns 400 with field errors
- [x] 4.4 Verify alignment with `dto-validation` spec requirements

## 5. Documentation
- [x] 5.1 Add XML comments to validator classes
- [x] 5.2 Update any relevant README or inline documentation
