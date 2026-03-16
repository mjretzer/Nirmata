## 1. Implementation

### 1.1 Services Composition Root
- [x] 1.1.1 Create `nirmata.Services/Composition/` directory
- [x] 1.1.2 Create `ServiceCollectionExtensions.cs` with `AddnirmataServices()` method
- [x] 1.1.3 Register `IProjectService` → `ProjectService` in the extension method
- [x] 1.1.4 Register repository dependencies (or confirm they're registered elsewhere)

### 1.2 Update Service Interfaces to Return DTOs
- [x] 1.2.1 Modify `IProjectService` to return `ProjectDto` instead of `Project` entity
- [x] 1.2.2 Modify `CreateProjectAsync` to accept `ProjectCreateRequestDto` instead of `Project` entity
- [x] 1.2.3 Update return types for `GetAllProjectsAsync` and `SearchProjectsAsync` to use `ProjectDto`

### 1.3 Update Service Implementations
- [x] 1.3.1 Inject `IMapper` into `ProjectService` constructor
- [x] 1.3.2 Update `GetProjectByIdAsync` to map entity to `ProjectDto` before returning
- [x] 1.3.3 Update `CreateProjectAsync` to map request DTO to entity, then response to `ProjectResponseDto`
- [x] 1.3.4 Update `GetAllProjectsAsync` to return mapped `List<ProjectDto>`
- [x] 1.3.5 Update `SearchProjectsAsync` to return mapped `List<ProjectDto>`

### 1.4 Update API Composition
- [x] 1.4.1 Add `using nirmata.Services.Composition;` to `nirmata.Api/Program.cs`
- [x] 1.4.2 Replace inline `AddScoped<IProjectService, ProjectService>()` with `AddnirmataServices()`

## 2. Testing

### 2.1 Create Services Test Project
- [x] 2.1.1 Create `tests/nirmata.Services.Tests/` project
- [x] 2.1.2 Add project references: `nirmata.Services`, `nirmata.Data`, `nirmata.Data.Dto`
- [x] 2.1.3 Add test dependencies: `xunit`, `Microsoft.NET.Test.Sdk`, `Moq` or `NSubstitute`

### 2.2 Service Unit Tests
- [x] 2.2.1 Create `ProjectServiceTests` class
- [x] 2.2.2 Test `GetProjectByIdAsync` returns mapped DTO when project exists
- [x] 2.2.3 Test `GetProjectByIdAsync` throws `NotFoundException` when project not found
- [x] 2.2.4 Test `CreateProjectAsync` persists entity and returns response DTO
- [x] 2.2.5 Test `GetAllProjectsAsync` returns all projects as DTOs
- [x] 2.2.6 Test `SearchProjectsAsync` filters and returns matching DTOs

## 3. Verification

- [x] 3.1 Build succeeds: `dotnet build nirmata.slnx` (verified individual projects)
- [x] 3.2 All existing tests pass: `dotnet test` (4 API tests pass)
- [x] 3.3 New service tests pass: `dotnet test tests/nirmata.Services.Tests/` (6 tests pass)
- [x] 3.4 API resolves services: DI container initializes correctly
- [x] 3.5 No circular references: Project dependency graph is valid
