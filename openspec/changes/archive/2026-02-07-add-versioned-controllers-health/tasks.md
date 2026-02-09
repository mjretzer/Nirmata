## 1. Implementation

### 1.1 Base Controller
- [x] 1.1.1 Create `Gmsd.Api/Controllers/GmsdController.cs` base class
- [x] 1.1.2 Add common response helper methods (OkResult, NotFoundResult, etc.)
- [x] 1.1.3 Refactor `ProjectController` to inherit from `GmsdController`

### 1.2 Health Controller
- [x] 1.2.1 Create `Gmsd.Api/Controllers/HealthController.cs`
- [x] 1.2.2 Implement GET `/api/health` endpoint with detailed checks
- [x] 1.2.3 Add database connectivity health check
- [x] 1.2.4 Add response model with status, dependencies, and timing

### 1.3 Health Check Enhancements
- [x] 1.3.1 Create custom health check for database connectivity
- [x] 1.3.2 Register custom health checks in `Program.cs`
- [x] 1.3.3 Configure health check response writer for detailed JSON output

## 2. Testing

### 2.1 Integration Tests
- [x] 2.1.1 Add health endpoint test to `Gmsd.Api.Tests/ProjectEndpointsTests.cs` (or create `HealthEndpointsTests.cs`)
- [x] 2.1.2 Test GET `/health` returns 200 OK
- [x] 2.1.3 Test GET `/api/health` returns detailed health JSON
- [x] 2.1.4 Test health response includes database status
- [x] 2.1.5 Verify v1 Project endpoints integration tests exist and pass

## 3. Verification

- [x] 3.1 Build succeeds: `dotnet build Gmsd.slnx`
- [x] 3.2 All tests pass: `dotnet test`
- [x] 3.3 Local run: `dotnet run --project Gmsd.Api` starts successfully
- [x] 3.4 Swagger loads: Navigate to `/swagger` and verify v1 endpoints documented
- [x] 3.5 Health endpoints work: GET `/health` and `/api/health` return expected responses
