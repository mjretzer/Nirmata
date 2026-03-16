# Tasks: Add Direct Agent Runner for MVP

## 1. DI Wiring and Composition
- [x] 1.1 Create `nirmata.Web/Composition/ServiceCollectionExtensions.cs` with `AddnirmataAgents()` call
- [x] 1.2 Configure workspace path resolution for Web project (use temp path or configurable)
- [x] 1.3 Add required package references to `nirmata.Agents` in `nirmata.Web.csproj`
- [x] 1.4 Verify service starts without Windows Service dependency

## 2. Direct Agent Runner
- [x] 2.1 Create `nirmata.Web/AgentRunner/WorkflowClassifier.cs` class
- [x] 2.2 Implement `ExecuteAsync()` method that wraps `IOrchestrator.ExecuteAsync()`
- [x] 2.3 Add input normalization for run commands
- [x] 2.4 Handle correlation ID generation/tracking
- [x] 2.5 Write unit tests for WorkflowClassifier

## 3. Runs Dashboard (List Page)
- [x] 3.1 Create `nirmata.Web/Pages/Runs/Index.cshtml` with page model
- [x] 3.2 Implement run list query (via `IRunRepository` or evidence folder enumeration)
- [x] 3.3 Display columns: Run ID, Status, Start Time, End Time, Current Phase
- [x] 3.4 Add empty state when no runs exist
- [x] 3.5 Add link to run detail page
- [x] 3.6 Write integration tests for Runs/Index page

## 4. Run Detail Page
- [x] 4.1 Create `nirmata.Web/Pages/Runs/Details.cshtml` with page model
- [x] 4.2 Display run metadata (ID, status, timestamps, correlation ID)
- [x] 4.3 Display execution logs (read from `.aos/evidence/runs/{runId}/logs/`)
- [x] 4.4 Display artifact pointers (summary.json, commands.json)
- [x] 4.5 Add "Back to Runs" navigation
- [x] 4.6 Handle 404 when run not found
- [x] 4.7 Write integration tests for Runs/Details page

## 5. Navigation Integration
- [x] 5.1 Add "Runs" link to main navigation in `_Layout.cshtml`
- [x] 5.2 Verify navigation renders correctly on all pages

## 6. Validation
- [x] 6.1 Verify can execute no-op command against test workspace via WorkflowClassifier
- [x] 6.2 Verify runs dashboard displays run status correctly
- [x] 6.3 Verify run detail page shows logs/artifacts correctly
- [x] 6.4 Run full test suite (`dotnet test`)
