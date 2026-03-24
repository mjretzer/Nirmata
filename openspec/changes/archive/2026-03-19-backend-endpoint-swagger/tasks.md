## 1. Data Access and Models

- [x] 1.1 Create project-related DTOs in `src/nirmata.Data.Dto/Models` and `Requests`
    - [x] 1.1.1 Create `ProjectUpdateRequestDto` in `src/nirmata.Data.Dto/Requests/Projects/` with `Name` property.
    - [x] 1.1.2 Create `ProjectSearchRequestDto` in `src/nirmata.Data.Dto/Requests/Projects/` for pagination/filtering (e.g., `SearchTerm`, `PageNumber`, `PageSize`).
- [x] 1.2 Implement FluentValidation for project requests in `src/nirmata.Data.Dto/Validators`
    - [x] 1.2.1 Create `ProjectUpdateRequestValidator` in `src/nirmata.Data.Dto/Validators/Projects/`.
    - [x] 1.2.2 Create `ProjectSearchRequestValidator` in `src/nirmata.Data.Dto/Validators/Projects/`.
- [x] 1.3 Enhance `IProjectRepository` and its implementation in `src/nirmata.Data/Repositories` with necessary CRUD and search methods
    - [x] 1.3.1 Add `Update(Project project)` and `DeleteAsync(string projectId)` to `IProjectRepository`.
    - [x] 1.3.2 Add `SearchAsync(...)` or `GetAllAsync(...)` with pagination support to `IProjectRepository`.
    - [x] 1.3.3 Implement new methods in `ProjectRepository.cs`.
- [x] 1.4 Update `nirmataDbContext` if any model adjustments are required for the repository enhancements
    - [x] 1.4.1 (Optional) Add audit properties (CreatedAt, UpdatedAt) to `Project` entity if required for the swagger endpoints.

## 2. Business Logic Layer

- [x] 2.1 Update `IProjectService` in `src/nirmata.Services/Interfaces`
    - [x] 2.1.1 Add `UpdateProjectAsync(string projectId, ProjectUpdateRequestDto request)` method.
    - [x] 2.1.2 Add `DeleteProjectAsync(string projectId)` method.
    - [x] 2.1.3 Update `SearchProjectsAsync` to use `ProjectSearchRequestDto` for pagination.
- [x] 2.2 Implement new logic in `ProjectService` (`src/nirmata.Services/Implementations`)
    - [x] 2.2.1 Implement `UpdateProjectAsync` with AutoMapper and repository `Update`.
    - [x] 2.2.2 Implement `DeleteProjectAsync` using repository `DeleteAsync`.
    - [x] 2.2.3 Refactor `GetAllProjectsAsync` and `SearchProjectsAsync` to use `IProjectRepository` instead of direct `DbContext` access.
- [x] 2.3 Verify service registration in `src/nirmata.Services/Composition/ServiceCollectionExtensions.cs`
    - [x] 2.3.1 Ensure `IProjectRepository` and `ProjectRepository` are registered (currently missing from this file).
    - [x] 2.3.2 Ensure `IProjectService` and `ProjectService` are registered.

## 3. Controllers and Endpoints

- [x] 3.1 Implement `V1/ProjectController.cs` in `src/nirmata.Api/Controllers`
    - [x] 3.1.1 Add `PUT` endpoint for `UpdateProject`.
    - [x] 3.1.2 Add `DELETE` endpoint for `DeleteProject`.
    - [x] 3.1.3 Update `GET` (ListAll) and `GET` (Search) to support pagination parameters via `ProjectSearchRequestDto`.
    - [x] 3.1.4 Add Swagger documentation attributes (e.g., `[ProducesResponseType]`) to all actions.
- [x] 3.2 Implement `HealthController.cs` in `src/nirmata.Api/Controllers` for detailed health checks (including DB)
    - [x] 3.2.1 Implement `GetHealthAsync` method to return health status.
    - [x] 3.2.2 Implement `GetDetailedHealthAsync` method to return detailed health status.
- [x] 3.3 Configure framework-level health checks in `src/nirmata.Api/Program.cs` via `MapHealthChecks("/health")`
    - [x] 3.3.1 Configure health checks for DB connection.
    - [x] 3.3.2 Configure health checks for other dependencies.

## 4. Documentation and Verification

- [x] 4.1 Update `src/nirmata.Api/nirmata.Api.http` with example requests for the new endpoints
- [x] 4.2 Verify Swagger UI at `/swagger` to ensure all endpoints and models are correctly documented
- [x] 4.3 Manually test each endpoint (GET, POST, search, health) using Swagger or `.http` file
