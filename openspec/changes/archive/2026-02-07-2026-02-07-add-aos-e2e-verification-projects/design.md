# Design: E2E Test Infrastructure for AOS

**Change ID:** `2026-02-07-add-aos-e2e-verification-projects`

---

## Goals

1. Prove AOS works end-to-end with real filesystem artifacts
2. Enable deterministic, repeatable E2E tests
3. Separate test infrastructure from product/engine code

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    E2E Test Layer                                │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  TSK-00A        │  │  TSK-00B        │  │  TSK-00C        │ │
│  │  TestTargets    │  │  Init E2E       │  │  Control Loop   │ │
│  │  (fixtures)     │  │  (validation)   │  │  (orchestration)│ │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘ │
└───────────┼────────────────────┼────────────────────┼──────────┘
            │                    │                    │
            ▼                    ▼                    ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Test Harness Layer                            │
│         AosTestHarness, AssertAosLayout, StateReader             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Engine Layer (existing)                       │
│    Gmsd.Aos (workspace)  +  Gmsd.Agents (workflows)             │
└─────────────────────────────────────────────────────────────────┘
```

---

## TestTarget Fixture System

### Design Decisions

1. **Disposable repos in %TEMP%** — Each test gets a fresh folder named `fixture-{guid}/` under `%TEMP%` to prevent test pollution.

2. **Minimal project templates** — Templates contain just enough to be valid:
   - Single `.csproj` with minimal dependencies
   - Single `.cs` file with minimal code
   - No build-time dependencies that slow tests

3. **Automatic cleanup** — Using `IDisposable` pattern in `FixtureRepo` to delete temp folders after tests.

4. **Deterministic GUIDs** — When possible, use seed-based GUIDs for reproducible test runs.

### FixtureRepo API

```csharp
public sealed class FixtureRepo : IDisposable
{
    public string RootPath { get; }
    
    // Creates %TEMP%/fixture-{guid}/ with template files
    public static FixtureRepo Create(string templateName = "minimal");
    
    // IDisposable cleans up temp folder
    public void Dispose();
}
```

---

## Test Harness API

### AosTestHarness

Provides high-level operations for driving AOS from tests:

```csharp
public sealed class AosTestHarness
{
    public AosTestHarness(string repoRoot);
    
    // Run AOS commands (CLI or in-proc)
    public Task<RunResult> RunAsync(string command, params string[] args);
    
    // Assert helpers
    public void AssertLayout();
    public T ReadState<T>(string relativePath);
    public IReadOnlyList<EventEntry> ReadEventsTail(int count);
}
```

### Harness supports two execution modes

1. **CLI mode** — Spawns `aos` as subprocess (tests real CLI behavior)
2. **In-proc mode** — Routes commands through `ICommandRouter` (faster, for CI)

Selection via constructor or environment variable.

---

## E2E Test Patterns

### Pattern 1: Bootstrap → Assert

```csharp
[Fact]
public async Task Init_CreatesValidWorkspace()
{
    using var fixture = FixtureRepo.Create();
    var harness = new AosTestHarness(fixture.RootPath);
    
    // Act
    var result = await harness.RunAsync("init");
    
    // Assert
    result.ExitCode.Should().Be(0);
    harness.AssertLayout();  // All 6 layers exist
}
```

### Pattern 2: Multi-Phase Scenario

```csharp
[Fact]
[Trait("Category", "E2E")]
public async Task FullControlLoop_ExecutesEndToEnd()
{
    using var fixture = FixtureRepo.Create();
    var harness = new AosTestHarness(fixture.RootPath);
    
    // 1. Bootstrap
    await harness.RunAsync("init");
    await harness.RunAsync("spec", "create", "--name", "TestProject");
    await harness.RunAsync("roadmap", "generate");
    
    // 2. Plan
    await harness.RunAsync("plan", "create", "--phase", "PH-001");
    
    // 3. Execute
    var run = await harness.RunAsync("execute-plan");
    
    // 4. Verify
    var verify = await harness.RunAsync("verify-work");
    
    // 5. Assert state transitions
    var state = harness.ReadState<RunState>(".aos/state/runs/latest.json");
    state.Status.Should().Be("completed");
}
```

---

## Project Structure

```
tests/
├── TestTargets/
│   ├── TestTargets.csproj
│   ├── FixtureRepo.cs
│   └── Templates/
│       └── minimal/
│           ├── Project.csproj.template
│           └── Program.cs.template
├── Gmsd.Aos.Tests/
│   └── E2E/
│       ├── Harness/
│       │   ├── AosTestHarness.cs
│       │   ├── AssertAosLayout.cs
│       │   ├── StateReader.cs
│       │   └── EventLogReader.cs
│       └── InitVerification/
│           ├── InitWorkspaceTests.cs
│           ├── InitIdempotencyTests.cs
│           └── ValidationGateTests.cs
└── Gmsd.Agents.Tests/
    └── E2E/
        └── ControlLoop/
            ├── FullControlLoopTests.cs
            └── TestScenarioBuilder.cs
```

---

## Trade-offs

| Approach | Pros | Cons |
|----------|------|------|
| CLI subprocess | Tests real CLI surface | Slower, process overhead |
| In-proc router | Fast, debuggable | Doesn't test CLI parsing |
| Decision | Support both, default to CLI for E2E | |

| Approach | Pros | Cons |
|----------|------|------|
| Real temp folders | Tests real filesystem I/O | Cleanup complexity |
| Mock filesystem | Fast, no cleanup | Doesn't catch real I/O issues |
| Decision | Use real temp folders | |

---

## CI Integration

E2E tests run with `[Trait("Category","E2E")]`:

```yaml
# CI pipeline
- name: Fast tests
  run: dotnet test --filter "Category!=E2E"

- name: E2E tests
  run: dotnet test --filter "Category=E2E"
  timeout-minutes: 10
```

---

## Future Extensions

- Git integration tests (conditional on git availability)
- Long-running pause/resume scenarios
- Multi-phase roadmap execution tests
