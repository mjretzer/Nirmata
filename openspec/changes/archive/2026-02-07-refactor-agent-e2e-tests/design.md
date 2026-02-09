# Design: Agent E2E Test Refactoring

## Architecture Overview

### Current Broken Architecture
```
Tests/
├── Fakes/                    # Incomplete, mismatched interfaces
│   ├── FakeEventStore.cs     # Uses old StateEventEntry ctor
│   └── FakeStateStore.cs     # Uses old StateEventTailResponse props
├── Workflows/Planning/       # Wrong namespace (Workflows → Execution)
├── Execution/Planning/       # Mix of old/new patterns
└── 188 compilation errors
```

### Proposed Architecture
```
Tests/
├── Contracts/                # NEW: Interface compliance tests
│   └── InterfaceComplianceTests.cs
├── Fixtures/                 # NEW: Stable test infrastructure
│   ├── AosTestWorkspace.cs   # Disposable .aos/ workspace
│   ├── HandlerTestHost.cs   # DI container for handler tests
│   └── AutoFakeBuilder.cs   # Reflection-based fake generator
├── Fakes/                    # FIXED: Interface-compliant fakes
│   ├── Base/
│   │   └── FakeBase.cs      # Common fake utilities
│   ├── Aos/
│   │   ├── FakeEventStore.cs
│   │   ├── FakeStateStore.cs
│   │   └── FakeWorkspace.cs
│   └── Agents/
│       ├── FakeLlmProvider.cs
│       └── FakeRunLifecycleManager.cs
├── Integration/              # E2E tests (filesystem, real DI)
│   └── Orchestrator/
│       └── OrchestratorEndToEndTests.cs
└── Workflows/                # Removed - consolidated to Execution/
```

## Key Design Decisions

### 1. Contract-First Testing

**Problem**: Tests break when implementation changes.
**Solution**: Test interfaces, not implementations.

```csharp
// Contract test ensures fake matches interface
[Fact]
public void FakeEventStore_ImplementsIEventStore()
{
    typeof(FakeEventStore).Should().Implement<IEventStore>();
}
```

**Benefit**: Breaking interface change = immediate test failure, not compilation error.

### 2. Reflection-Based Fake Validation

**Problem**: Fakes diverge from interfaces silently.
**Solution**: Auto-check fake compliance.

```csharp
public static class FakeValidator
{
    public static void ValidateFake<TFake, TInterface>()
        where TFake : TInterface
    {
        // Verify all interface methods are implemented
        // Verify method signatures match
        // Verify return types are compatible
    }
}
```

### 3. Test Workspace Builder

**Problem**: Tests manually create .aos/ structure, inconsistent.
**Solution**: Fluent builder pattern.

```csharp
using var workspace = new AosTestWorkspaceBuilder()
    .WithProject(name: "Test", description: "Test project")
    .WithRoadmap(milestones: ["M1", "M2"])
    .WithState(cursor: new StateCursor { PhaseId = "Implementation" })
    .Build();
```

### 4. Handler Test Host

**Problem**: Orchestrator needs 7 handlers, tests manually mock all.
**Solution**: DI container with sensible defaults.

```csharp
using var host = new HandlerTestHost()
    .WithGatingResult(targetPhase: "Roadmapper")
    .WithCommandResult(success: true, output: "created");

var orchestrator = host.GetService<IOrchestrator>();
var result = await orchestrator.ExecuteAsync(intent);
```

### 5. Layered Test Strategy

| Layer | Scope | Speed | Count | Purpose |
|-------|-------|-------|-------|---------|
| Contract | Interface boundaries | Fast | ~20 | Prevent drift |
| Workflow | Orchestrator routing | Medium | ~10 | Verify gates |
| Integration | Full filesystem | Slow | ~5 | Verify evidence |

## Implementation Phases

### Phase 1: Foundation (Day 1-2)
1. Create `FakeValidator` with reflection checks
2. Fix `FakeEventStore` to match `IEventStore`
3. Fix `FakeStateStore` to match `IStateStore`
4. Fix `FakeWorkspace` to match `IWorkspace`

### Phase 2: Handler Fakes (Day 3)
1. Create `FakeSymbolCacheBuilder`
2. Create `FakeCodebaseScanner` with progress support
3. Fix `FakeRunLifecycleManager` with `RunContext` property

### Phase 3: Test Host (Day 4)
1. Build `HandlerTestHost` DI container
2. Implement `AosTestWorkspaceBuilder`
3. Create `AutoFakeBuilder` for handler mocks

### Phase 4: E2E Tests (Day 5)
1. Rewrite `OrchestratorEndToEndTests`
2. Rewrite `GatingEngineIntegrationTests`
3. Add contract tests for all fakes

### Phase 5: Consolidation (Day 6)
1. Remove broken test files
2. Move surviving tests to correct namespaces
3. Add CI integration

## Technical Details

### Fake Event Store Fix

**Current (broken)**:
```csharp
new StateEventEntry(
    LineNumber: 1,           // ← Positional record ctor
    TimestampUtc: now,       // ← Property doesn't exist
    EventType: "test",         // ← Property doesn't exist
    Data: payload             // ← Wrong property name
)
```

**Fixed**:
```csharp
new StateEventEntry
{
    LineNumber = 1,
    Payload = payload         // ← Correct property name
}
```

### Orchestrator Constructor Fix

**Current (broken)**:
```csharp
new Orchestrator(
    gatingEngine,
    commandRouter,
    workspace,
    specStore,
    stateStore,
    validator,
    runLifecycleManager,
    interviewerHandler        // ← Missing 6 handlers!
);
```

**Fixed**:
```csharp
new Orchestrator(
    gatingEngine,
    commandRouter,
    workspace,
    specStore,
    stateStore,
    validator,
    runLifecycleManager,
    interviewerHandler,
    roadmapperHandler,
    phasePlannerHandler,
    taskExecutorHandler,
    verifierHandler,
    atomicGitCommitterHandler  // ← All 7 handlers present
);
```

### Test Host Pattern

```csharp
public class HandlerTestHost : IDisposable
{
    private readonly ServiceCollection _services = new();
    private ServiceProvider? _provider;

    public HandlerTestHost WithDefaultFakes()
    {
        _services.AddSingleton<IEventStore, FakeEventStore>();
        _services.AddSingleton<IStateStore, FakeStateStore>();
        _services.AddSingleton<IWorkspace, FakeWorkspace>();
        // ... etc
        return this;
    }

    public HandlerTestHost WithMock<TService>(Mock<TService> mock)
        where TService : class
    {
        _services.AddSingleton<TService>(mock.Object);
        return this;
    }

    public TService GetService<TService>()
        where TService : notnull
    {
        _provider ??= _services.BuildServiceProvider();
        return _provider.GetRequiredService<TService>();
    }

    public void Dispose() => _provider?.Dispose();
}
```

## Verification Strategy

### Compile-Time Checks
- `FakeValidator.ValidateAllFakes()` runs in static ctor
- CI build fails if fakes don't match interfaces

### Run-Time Checks
- Each E2E test verifies evidence folder structure
- JSON schema validation for written artifacts
- Deterministic output checks (write-read-write yields same bytes)

## Rollback Plan
If refactoring introduces instability:
1. Revert to `Gmsd.Agents.Tests` backup
2. Apply fixes incrementally (namespace → constructor → fakes)
3. Validate each batch before next

## Success Metrics
- [ ] `dotnet build tests\Gmsd.Agents.Tests` → 0 errors
- [ ] `dotnet test --filter "E2E"` → All pass
- [ ] `dotnet test` → Completes in <30 seconds
- [ ] New test added → Follows pattern, compiles immediately
