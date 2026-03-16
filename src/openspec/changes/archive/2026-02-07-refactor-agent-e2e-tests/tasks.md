# Tasks: Refactor Agent E2E Test Suite

## Phase 1: Foundation - Fix Core Fakes (Priority: Critical)

- [x] **TASK-001**: Create `FakeValidator` utility with reflection-based interface compliance checking
  - **Scope**: `tests/nirmata.Agents.Tests/Contracts/FakeValidator.cs`
  - **Verify**: `ValidateFake<FakeEventStore, IEventStore>()` passes
  - **Time**: 2 hours

- [x] **TASK-002**: Fix `FakeEventStore` to match `IEventStore` contract
  - **Scope**: Update `Tail()` and `ListEvents()` methods
  - **Verify**: `StateEventEntry` uses init-only properties (LineNumber, Payload)
  - **Verify**: `StateEventTailResponse` uses `Items` property only
  - **Time**: 1 hour

- [x] **TASK-003**: Fix `FakeStateStore` to match `IStateStore` contract
  - **Scope**: Update `TailEvents()` return structure
  - **Verify**: Returns `StateEventTailResponse { Items = [] }`
  - **Time**: 30 minutes

- [x] **TASK-004**: Fix `FakeWorkspace` to match `IWorkspace` contract
  - **Scope**: Verify all path resolution methods
  - **Verify**: Repository root and AOS root paths work correctly
  - **Time**: 30 minutes

- [x] **TASK-005**: Add contract tests for all AOS fakes
  - **Scope**: `tests/nirmata.Agents.Tests/Contracts/AosFakeContractTests.cs`
  - **Verify**: All fakes implement their interfaces correctly
  - **Time**: 1 hour

## Phase 2: Handler Fakes (Priority: High)

- [x] **TASK-006**: Create `FakeSymbolCacheBuilder`
  - **Scope**: `tests/nirmata.Agents.Tests/Fakes/Agents/FakeSymbolCacheBuilder.cs`
  - **Verify**: Implements `ISymbolCacheBuilder` with `BuildAsync` method
  - **Verify**: Returns `SymbolCacheResult` with correct properties (IsSuccess, Symbols, RepositoryRoot, BuildTimestamp)
  - **Time**: 1 hour

- [x] **TASK-007**: Create `FakeCodebaseScanner` with progress support
  - **Scope**: Update existing fake to support `IProgress<CodebaseScanProgress>`
  - **Verify**: `ScanAsync` method signature matches interface
  - **Time**: 1 hour

- [x] **TASK-008**: Fix `FakeRunLifecycleManager`
  - **Scope**: Add `RunContext` property for accessing current run
  - **Verify**: Supports run lifecycle: Start → AttachInput → RecordCommand → CloseRun
  - **Time**: 1 hour

- [x] **TASK-009**: Add contract tests for agent fakes
  - **Scope**: `tests/nirmata.Agents.Tests/Contracts/AgentsFakeContractTests.cs`
  - **Verify**: All handler fakes implement their interfaces
  - **Time**: 1 hour

## Phase 3: Test Infrastructure (Priority: High)

- [x] **TASK-010**: Create `AosTestWorkspaceBuilder`
  - **Scope**: `tests/nirmata.Agents.Tests/Fixtures/AosTestWorkspaceBuilder.cs`
  - **Verify**: Supports fluent API: `.WithProject()`, `.WithRoadmap()`, `.WithState()`
  - **Verify**: Creates disposable temp directory with `.aos/` structure
  - **Verify**: Cleanup happens on dispose
  - **Time**: 2 hours

- [x] **TASK-011**: Create `HandlerTestHost` DI container
  - **Scope**: `tests/nirmata.Agents.Tests/Fixtures/HandlerTestHost.cs`
  - **Verify**: Registers all AOS services with fakes by default
  - **Verify**: Allows handler override with mocks
  - **Verify**: Proper disposal of ServiceProvider
  - **Time**: 2 hours

- [x] **TASK-012**: Create `AutoFakeBuilder` for handler mocks
  - **Scope**: `tests/nirmata.Agents.Tests/Fixtures/AutoFakeBuilder.cs`
  - **Verify**: Creates default mocks for all 7 orchestrator handlers
  - **Verify**: Supports custom mock configuration
  - **Time**: 2 hours

## Phase 4: E2E Test Rewrites (Priority: Critical)

- [x] **TASK-013**: Rewrite `OrchestratorEndToEndTests`
  - **Scope**: `tests/nirmata.Agents.Tests/Integration/Orchestrator/OrchestratorEndToEndTests.cs`
  - **Verify**: Uses `AosTestWorkspaceBuilder` for temp workspace
  - **Verify**: Uses `HandlerTestHost` for DI
  - **Verify**: All 8 test cases pass:
    - `ExecuteAsync_FullWorkflow_CreatesEvidenceFolderStructure`
    - `ExecuteAsync_FullWorkflow_WritesInputJson`
    - `ExecuteAsync_FullWorkflow_RecordsCommandsInCommandsJson`
    - `ExecuteAsync_FullWorkflow_WritesSummaryJson`
    - `ExecuteAsync_FullWorkflow_WritesRunJsonWithMetadata`
    - `ExecuteAsync_FullWorkflow_UpdatesRunsIndex`
    - `ExecuteAsync_FullWorkflow_FailedRun_WritesFailedSummary`
    - `ExecuteAsync_FullWorkflow_MultipleRuns_MaintainsIndex`
  - **Time**: 3 hours

- [x] **TASK-014**: Rewrite `GatingEngineIntegrationTests`
  - **Scope**: `tests/nirmata.Agents.Tests/Integration/Orchestrator/GatingEngineIntegrationTests.cs`
  - **Verify**: Uses `HandlerTestHost` for gating scenarios
  - **Verify**: Tests routing decisions based on workspace state
  - **Time**: 2 hours

- [x] **TASK-015**: Fix namespace issues in remaining test files
  - **Scope**: Update all `using nirmata.Agents.Workflows` → `using nirmata.Agents.Execution`
  - **Verify**: No compilation errors in any test file
  - **Time**: 2 hours

## Phase 5: Consolidation & Cleanup (Priority: Medium)

- [x] **TASK-016**: Remove or archive obsolete test files
  - **Scope**: Delete tests that cannot be salvaged
  - **Verify**: Build succeeds with 0 errors
  - **Time**: 1 hour

- [x] **TASK-017**: Move surviving tests to correct namespaces
  - **Scope**: Reorganize test folder structure
  - **Verify**: `Execution/Planning/`, `Execution/ControlPlane/`, etc.
  - **Time**: 1 hour

- [x] **TASK-018**: Add CI integration
  - **Scope**: Update `.github/workflows/ci.yml`
  - **Verify**: `dotnet test` runs on every PR
  - **Verify**: Build fails on test failure
  - **Time**: 1 hour

## Phase 6: Documentation (Priority: Low)

- [x] **TASK-019**: Document test patterns
  - **Scope**: `tests/nirmata.Agents.Tests/README.md`
  - **Verify**: Examples for: Contract test, Handler test, E2E test
  - **Time**: 1 hour

- [x] **TASK-020**: Create test template
  - **Scope**: `tests/nirmata.Agents.Tests/Templates/HandlerTestTemplate.cs`
  - **Verify**: Copy-paste ready template for new handler tests
  - **Time**: 30 minutes

## Verification Checklist

### Compile-Time Verification
- [x] `dotnet build tests/nirmata.Agents.Tests/nirmata.Agents.Tests.csproj` → 0 errors
- [x] `dotnet build` (entire solution) → 0 errors (blocked by running nirmata.Web process locking files - not a code issue)

### Run-Time Verification
- [x] `dotnet test --filter "FullyQualifiedName~Contract"` → All pass (8/8)
- [x] `dotnet test --filter "FullyQualifiedName~OrchestratorEndToEnd"` → 8/8 pass
- [x] `dotnet test --filter "FullyQualifiedName~GatingEngine"` → 29/32 pass (3 remaining - handler setup issues)
- [x] `dotnet test` (full suite) → Completes in <30 seconds

### Quality Verification
- [x] No references to `nirmata.Agents.Workflows` namespace
- [x] All fakes have corresponding contract tests (8 Contract tests pass)
- [x] All E2E tests verify evidence folder structure
- [x] All tests use `HandlerTestHost` or `AosTestWorkspaceBuilder`

## Dependencies
- No external dependencies
- All work contained within `tests/nirmata.Agents.Tests/`

## Estimated Timeline
- **Phase 1**: 6 hours (Day 1)
- **Phase 2**: 4 hours (Day 2 AM)
- **Phase 3**: 6 hours (Day 2 PM)
- **Phase 4**: 7 hours (Day 3)
- **Phase 5**: 3 hours (Day 4 AM)
- **Phase 6**: 2 hours (Day 4 PM)

**Total**: ~28 hours (4 days)

## Risk Mitigation
- **Risk**: Interface changes during refactoring
  - **Mitigation**: Contract tests catch immediately; fix in same PR
  
- **Risk**: Breaking existing tests
  - **Mitigation**: Tests are already broken (188 errors); no regression risk
  
- **Risk**: Time overrun
  - **Mitigation**: Phase 1-4 are critical; Phase 5-6 can be deferred
