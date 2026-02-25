# Gmsd.Agents.Tests

Test suite for the GMSD Agent Plane. This project contains unit tests, integration tests, and end-to-end tests for all agent execution components.

## Test Patterns

### Contract Test

Contract tests verify that fake implementations correctly implement their interfaces using reflection-based validation.

**When to use**: Whenever you create or modify a fake, add a corresponding contract test.

```csharp
using Xunit;
using Gmsd.Agents.Tests.Contracts;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Public;

public class AosFakeContractTests
{
    [Fact]
    public void ValidateFake_FakeEventStore_ImplementsIEventStore()
    {
        FakeValidator.ValidateFake<FakeEventStore, IEventStore>();
    }
}
```

**Key points**:
- Uses `FakeValidator.ValidateFake<TFake, TInterface>()` for reflection-based checking
- Catches signature mismatches at compile/test time
- Place in `Contracts/` folder
- Run with: `dotnet test --filter "FullyQualifiedName~Contract"`

---

### Handler Test

Handler tests verify individual command handlers in isolation using fakes for dependencies.

**When to use**: Testing handler logic, command parsing, error handling, and state transitions.

```csharp
using Xunit;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Contracts.Commands;

public class MyHandlerTests
{
    private readonly FakeDependency _fakeDependency = new();
    private readonly FakeWorkspace _workspace = new();
    private readonly FakeStateStore _stateStore = new();

    private MyHandler CreateHandler()
    {
        return new MyHandler(_fakeDependency, _workspace, _stateStore);
    }

    [Fact]
    public async Task HandleAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";

        _fakeDependency.SetupResult(expectedResult);

        var request = new CommandRequest
        {
            Group = "spec",
            Command = "mycommand",
            Options = new Dictionary<string, string?>
            {
                ["key"] = "value"
            }
        };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task HandleAsync_InvalidRequest_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";

        var request = new CommandRequest { Group = "spec", Command = "mycommand" };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("error message", result.ErrorOutput);
    }
}
```

**Key points**:
- Create handler via factory method for consistent setup
- Use fakes from `Gmsd.Agents.Tests.Fakes` namespace
- Test both success and failure paths
- Verify state store updates when applicable
- Place in `Execution/<Category>/` folder matching handler location

---

### E2E Test

End-to-end tests verify the full orchestrator workflow using real filesystem and DI container.

**When to use**: Testing complete workflows, evidence folder structure, file I/O, and integration between components.

```csharp
using FluentAssertions;
using System.Text.Json;
using Xunit;
using Gmsd.Agents.Tests.Fixtures;
using Gmsd.Agents.Tests.Integration.Orchestrator;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Aos.Public;

public class OrchestratorEndToEndTests : IDisposable
{
    private readonly AosTestWorkspaceBuilder _workspaceBuilder;
    private readonly HandlerTestHost _testHost;
    private readonly Orchestrator _sut;

    public OrchestratorEndToEndTests()
    {
        _workspaceBuilder = new AosTestWorkspaceBuilder()
            .WithProject("Test Project", "Test project description");

        var workspace = _workspaceBuilder.Build();

        _testHost = new HandlerTestHost(workspace.RepositoryRootPath);
        _testHost.OverrideWithInstance<IWorkspace>(workspace);

        _sut = _testHost.GetRequiredService<IOrchestrator>() as Orchestrator
            ?? throw new InvalidOperationException("Could not resolve Orchestrator");
    }

    public void Dispose()
    {
        _testHost.Dispose();
        _workspaceBuilder.Dispose();
    }

    [Fact]
    public async Task ExecuteAsync_FullWorkflow_CreatesEvidenceFolderStructure()
    {
        // Arrange
        var intent = new WorkflowIntent
        {
            InputRaw = "test input",
            CorrelationId = "corr-e2e-001"
        };

        // Act
        var result = await _sut.ExecuteAsync(intent);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.RunId.Should().NotBeNullOrEmpty();

        // Verify evidence folder structure
        var evidenceFolder = Path.Combine(
            _workspaceBuilder.RepositoryRootPath,
            ".aos", "evidence", "runs", result.RunId!);

        Directory.Exists(evidenceFolder).Should().BeTrue();
        File.Exists(Path.Combine(evidenceFolder, "run.json")).Should().BeTrue();
        File.Exists(Path.Combine(evidenceFolder, "commands.json")).Should().BeTrue();
        File.Exists(Path.Combine(evidenceFolder, "summary.json")).Should().BeTrue();
    }
}
```

**Key points**:
- Use `AosTestWorkspaceBuilder` for temp workspace creation
- Use `HandlerTestHost` for DI container with fakes
- Implement `IDisposable` for cleanup
- Verify filesystem state in addition to return values
- Place in `Integration/` folder

## Test Fixtures

### AosTestWorkspaceBuilder

Creates disposable temporary workspaces with `.aos/` structure for filesystem-based tests.

```csharp
using var workspace = new AosTestWorkspaceBuilder()
    .WithProject("My Project", "Description")
    .WithRoadmap()
    .WithState(cursor: "task-1")
    .Build();

// Use workspace.RepositoryRootPath for file operations
// Cleanup happens automatically on dispose
```

### HandlerTestHost

DI container for handler tests with fake registrations and override capabilities.

```csharp
using var host = new HandlerTestHost(workspacePath);

// Get services
var handler = host.GetRequiredService<IMyHandler>();

// Override with mock
var mock = new Mock<IMyService>();
host.OverrideWithInstance<IMyService>(mock.Object);

// Cleanup happens automatically on dispose
```

### AutoFakeBuilder

Creates default mocks for all 7 orchestrator handlers.

```csharp
var fakes = new AutoFakeBuilder()
    .WithDefaultHandlers()
    .WithCustomHandler<Mock<IMyHandler>>(mock =>
    {
        mock.Setup(...).Returns(...);
    })
    .Build();
```

## Running Tests

```bash
# All tests
dotnet test

# Contract tests only
dotnet test --filter "FullyQualifiedName~Contract"

# E2E tests only
dotnet test --filter "FullyQualifiedName~OrchestratorEndToEnd"

# Specific handler tests
dotnet test --filter "FullyQualifiedName~AtomicGitCommitterHandler"

# With verbosity
dotnet test --logger "console;verbosity=detailed"
```

## Project Structure

```
tests/Gmsd.Agents.Tests/
├── Configuration/          # Options and configuration tests
├── Contracts/              # Interface contract validation tests
├── Execution/              # Handler unit tests
│   ├── Brownfield/         # Codebase analysis handlers
│   ├── ControlPlane/       # Orchestrator & gating
│   ├── Execution/          # Task execution handlers
│   ├── Planning/           # Roadmap/plan handlers
│   └── Verification/       # Verification handlers
├── Fakes/                  # Fake implementations for testing
├── Fixtures/               # Test fixtures and builders
├── Integration/            # E2E and integration tests
└── Templates/              # Copy-paste test templates
```

## Adding New Tests

1. **Contract test**: If adding/modifying a fake, add to `Contracts/`
2. **Handler test**: Create in `Execution/<Category>/` matching handler namespace
3. **E2E test**: Add to `Integration/` for full workflow tests

Use `Templates/HandlerTestTemplate.cs` as a starting point for new handler tests.
