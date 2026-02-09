using FluentAssertions;
using Gmsd.Agents.Execution.Brownfield.CodebaseScanner;
using Gmsd.Agents.Execution.Brownfield.MapValidator;
using Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Agents.Tests.Fakes;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Engine.Commands;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Catalogs;
using Gmsd.Aos.Public.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane;

/// <summary>
/// Integration tests for CodebaseMapperHandler working with Orchestrator gating.
/// Verifies that the handler is properly invoked through the orchestrator's dispatch flow
/// and respects gating decisions for codebase mapping operations.
/// </summary>
public class CodebaseMapperHandlerIntegrationTests : IDisposable
{
    private readonly FakeWorkspace _workspace;
    private readonly FakeRunLifecycleManager _runLifecycleManager;
    private readonly FakeCodebaseScanner _fakeScanner;
    private readonly FakeMapValidator _fakeValidator;
    private readonly FakeSymbolCacheBuilder _fakeSymbolCacheBuilder;
    private readonly CodebaseMapperHandler _handler;
    private readonly ServiceProvider _serviceProvider;
    private readonly ICommandRouter _commandRouter;

    public CodebaseMapperHandlerIntegrationTests()
    {
        _workspace = new FakeWorkspace();
        _runLifecycleManager = new FakeRunLifecycleManager();

        // Create fake implementations that can be controlled from tests
        _fakeScanner = new FakeCodebaseScanner();
        _fakeValidator = new FakeMapValidator();
        _fakeSymbolCacheBuilder = new FakeSymbolCacheBuilder();

        // Create handler with fakes
        _handler = new CodebaseMapperHandler(
            _fakeScanner,
            _fakeValidator,
            _fakeSymbolCacheBuilder,
            _workspace,
            _runLifecycleManager);

        // Set up DI container with command catalog registration
        var services = new ServiceCollection();
        services.AddSingleton<IWorkspace>(_workspace);
        services.AddSingleton<IRunLifecycleManager>(_runLifecycleManager);
        services.AddSingleton<ICodebaseScanner>(_fakeScanner);
        services.AddSingleton<IMapValidator>(_fakeValidator);
        services.AddSingleton<ISymbolCacheBuilder>(_fakeSymbolCacheBuilder);
        services.AddSingleton(_handler);

        // Register command catalog with codebase map command
        services.AddSingleton<CommandCatalog>(sp =>
        {
            var catalog = new CommandCatalog();
            var handler = sp.GetRequiredService<CodebaseMapperHandler>();

            catalog.Register(
                new CommandMetadata
                {
                    Group = "codebase",
                    Command = "map",
                    Id = "codebase.map",
                    Description = "Map codebase structure and generate intelligence pack"
                },
                async ctx =>
                {
                    var request = new CommandRequest
                    {
                        Group = "codebase",
                        Command = "map",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value)
                    };
                    var result = await handler.HandleAsync(
                        request,
                        "RUN-TEST-001",
                        ctx.CancellationToken);

                    return result.IsSuccess
                        ? Aos.Engine.Commands.Base.CommandResult.Success(result.Output)
                        : Aos.Engine.Commands.Base.CommandResult.Failure(
                            result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            return catalog;
        });

        services.AddSingleton<ICommandRouter>(sp =>
        {
            var catalog = sp.GetRequiredService<CommandCatalog>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            return new CommandRouter(catalog, workspace, null);
        });

        _serviceProvider = services.BuildServiceProvider();
        _commandRouter = _serviceProvider.GetRequiredService<ICommandRouter>();
    }

    public void Dispose()
    {
        _workspace.Dispose();
        _serviceProvider.Dispose();
    }

    [Fact]
    public async Task CommandRouter_RoutesCodebaseMapCommand_ToHandler()
    {
        // Arrange
        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = Array.Empty<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Set up scanner to return success
        _fakeScanner.SetScanResult(new CodebaseScanResult
        {
            IsSuccess = true,
            Solutions = new List<SolutionInfo> { new() { Name = "Test.sln", Path = "/test/Test.sln" } },
            Projects = new List<ProjectInfo> { new() { Name = "Test.csproj", Path = "/test/Test.csproj" } }
        });

        // Set up validator to return valid
        _fakeValidator.SetValidationResult(new MapValidationResult
        {
            IsValid = true,
            Summary = new MapValidationSummary { ArtifactsValidated = 1, ErrorCount = 0, WarningCount = 0, InfoCount = 0 }
        });

        // Act
        var result = await _commandRouter.RouteAsync(request, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("Codebase mapping completed successfully");
    }

    [Fact]
    public async Task Handler_WithNewRepository_TriggersScan()
    {
        // Arrange - ensure no codebase directory exists (new repo)
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        if (Directory.Exists(codebasePath))
        {
            Directory.Delete(codebasePath, true);
        }

        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = Array.Empty<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Set up scanner and validator
        _fakeScanner.SetScanResult(new CodebaseScanResult
        {
            IsSuccess = true,
            Solutions = new List<SolutionInfo> { new() { Name = "Test.sln", Path = "/test/Test.sln" } },
            Projects = new List<ProjectInfo> { new() { Name = "Test.csproj", Path = "/test/Test.csproj" } }
        });
        _fakeValidator.SetValidationResult(new MapValidationResult
        {
            IsValid = true,
            Summary = new MapValidationSummary { ArtifactsValidated = 1, ErrorCount = 0, WarningCount = 0, InfoCount = 0 }
        });

        // Act
        var result = await _handler.HandleAsync(request, "run-001", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("New repository");
        _fakeScanner.ScanWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_WithExistingCurrentMap_SkipsScan()
    {
        // Arrange - create existing codebase directory with map.json
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        Directory.CreateDirectory(codebasePath);
        var mapFilePath = Path.Combine(codebasePath, "map.json");
        var mapContent = new
        {
            schemaVersion = "gmsd:aos:schema:codebase-map:v1",
            version = "1.0.0",
            repository = _workspace.RepositoryRootPath,
            scanTimestamp = DateTimeOffset.UtcNow,
            summary = new { totalFiles = 10, totalProjects = 1 }
        };
        File.WriteAllText(mapFilePath, System.Text.Json.JsonSerializer.Serialize(mapContent));

        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = Array.Empty<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Act
        var result = await _handler.HandleAsync(request, "run-001", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("up to date");
        _fakeScanner.ScanWasCalled.Should().BeFalse();
    }

    [Fact]
    public async Task Handler_WithExplicitForceFlag_TriggersScan()
    {
        // Arrange - create existing codebase directory with map.json
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        Directory.CreateDirectory(codebasePath);
        var mapFilePath = Path.Combine(codebasePath, "map.json");
        var mapContent = new
        {
            schemaVersion = "gmsd:aos:schema:codebase-map:v1",
            version = "1.0.0",
            repository = _workspace.RepositoryRootPath,
            scanTimestamp = DateTimeOffset.UtcNow,
            summary = new { totalFiles = 10, totalProjects = 1 }
        };
        File.WriteAllText(mapFilePath, System.Text.Json.JsonSerializer.Serialize(mapContent));

        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = Array.Empty<string>(),
            Options = new Dictionary<string, string?> { { "force", "true" } }
        };

        // Set up scanner and validator
        _fakeScanner.SetScanResult(new CodebaseScanResult
        {
            IsSuccess = true,
            Solutions = new List<SolutionInfo>(),
            Projects = new List<ProjectInfo>()
        });
        _fakeValidator.SetValidationResult(new MapValidationResult
        {
            IsValid = true,
            Summary = new MapValidationSummary { ArtifactsValidated = 1, ErrorCount = 0, WarningCount = 0, InfoCount = 0 }
        });

        // Act
        var result = await _handler.HandleAsync(request, "run-001", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("Explicit request");
        _fakeScanner.ScanWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_WithStaleMap_TriggersScan()
    {
        // Arrange - create existing codebase directory with old map.json
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        Directory.CreateDirectory(codebasePath);
        var mapFilePath = Path.Combine(codebasePath, "map.json");
        var mapContent = new
        {
            schemaVersion = "gmsd:aos:schema:codebase-map:v1",
            version = "1.0.0",
            repository = _workspace.RepositoryRootPath,
            scanTimestamp = DateTimeOffset.UtcNow.AddHours(-48), // 48 hours old
            summary = new { totalFiles = 10, totalProjects = 1 }
        };
        File.WriteAllText(mapFilePath, System.Text.Json.JsonSerializer.Serialize(mapContent));

        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = Array.Empty<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Set up scanner and validator
        _fakeScanner.SetScanResult(new CodebaseScanResult
        {
            IsSuccess = true,
            Solutions = new List<SolutionInfo>(),
            Projects = new List<ProjectInfo>()
        });
        _fakeValidator.SetValidationResult(new MapValidationResult
        {
            IsValid = true,
            Summary = new MapValidationSummary { ArtifactsValidated = 1, ErrorCount = 0, WarningCount = 0, InfoCount = 0 }
        });

        // Act
        var result = await _handler.HandleAsync(request, "run-001", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("Stale map");
        _fakeScanner.ScanWasCalled.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_WhenScanFails_ReturnsFailure()
    {
        // Arrange
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        if (Directory.Exists(codebasePath))
        {
            Directory.Delete(codebasePath, true);
        }

        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = Array.Empty<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Set up scanner to fail
        _fakeScanner.SetScanResult(new CodebaseScanResult
        {
            IsSuccess = false,
            ErrorMessage = "Scan failed due to disk error"
        });

        // Act
        var result = await _handler.HandleAsync(request, "run-001", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("Codebase scan failed");
    }

    [Fact]
    public async Task Handler_WhenValidationFails_ReturnsFailure()
    {
        // Arrange
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        if (Directory.Exists(codebasePath))
        {
            Directory.Delete(codebasePath, true);
        }

        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = Array.Empty<string>(),
            Options = new Dictionary<string, string?>()
        };

        // Set up scanner to succeed but validator to fail
        _fakeScanner.SetScanResult(new CodebaseScanResult
        {
            IsSuccess = true,
            Solutions = new List<SolutionInfo>(),
            Projects = new List<ProjectInfo>()
        });
        _fakeValidator.SetValidationResult(new MapValidationResult
        {
            IsValid = false,
            Summary = new MapValidationSummary { ArtifactsValidated = 1, ErrorCount = 3, WarningCount = 2, InfoCount = 0 }
        });

        // Act
        var result = await _handler.HandleAsync(request, "run-001", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.ErrorOutput.Should().Contain("validation failed");
    }

    [Fact]
    public async Task Handler_RecordsCommandsInLifecycleManager()
    {
        // Arrange
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        if (Directory.Exists(codebasePath))
        {
            Directory.Delete(codebasePath, true);
        }

        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = Array.Empty<string>(),
            Options = new Dictionary<string, string?>()
        };

        _fakeScanner.SetScanResult(new CodebaseScanResult
        {
            IsSuccess = true,
            Solutions = new List<SolutionInfo>(),
            Projects = new List<ProjectInfo>()
        });
        _fakeValidator.SetValidationResult(new MapValidationResult
        {
            IsValid = true,
            Summary = new MapValidationSummary { ArtifactsValidated = 1, ErrorCount = 0, WarningCount = 0, InfoCount = 0 }
        });

        // Act
        await _handler.HandleAsync(request, "run-001", CancellationToken.None);

        // Assert
        var commands = _runLifecycleManager.GetRecordedCommands();
        commands.Should().Contain(c =>
            c.Group == "codebase" &&
            c.Command == "map" &&
            c.Status == "dispatched");
        commands.Should().Contain(c =>
            c.Group == "codebase" &&
            c.Command == "map" &&
            c.Status == "completed");
    }

    [Fact]
    public async Task Handler_WithCustomStaleThreshold_RespectsThreshold()
    {
        // Arrange - create map that is 12 hours old (default is 24 hours)
        var codebasePath = Path.Combine(_workspace.AosRootPath, "codebase");
        Directory.CreateDirectory(codebasePath);
        var mapFilePath = Path.Combine(codebasePath, "map.json");
        var mapContent = new
        {
            schemaVersion = "gmsd:aos:schema:codebase-map:v1",
            version = "1.0.0",
            repository = _workspace.RepositoryRootPath,
            scanTimestamp = DateTimeOffset.UtcNow.AddHours(-12),
            summary = new { totalFiles = 10, totalProjects = 1 }
        };
        File.WriteAllText(mapFilePath, System.Text.Json.JsonSerializer.Serialize(mapContent));

        // Request with 6 hour threshold (12 > 6, should trigger scan)
        var request = new CommandRequest
        {
            Group = "codebase",
            Command = "map",
            Arguments = new[] { "--stale-hours=6" },
            Options = new Dictionary<string, string?>()
        };

        _fakeScanner.SetScanResult(new CodebaseScanResult
        {
            IsSuccess = true,
            Solutions = new List<SolutionInfo>(),
            Projects = new List<ProjectInfo>()
        });
        _fakeValidator.SetValidationResult(new MapValidationResult
        {
            IsValid = true,
            Summary = new MapValidationSummary { ArtifactsValidated = 1, ErrorCount = 0, WarningCount = 0, InfoCount = 0 }
        });

        // Act
        var result = await _handler.HandleAsync(request, "run-001", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("Stale map");
        _fakeScanner.ScanWasCalled.Should().BeTrue();
    }

    // Fake implementations for testing

    private class FakeCodebaseScanner : ICodebaseScanner
    {
        private CodebaseScanResult? _nextResult;
        public bool ScanWasCalled { get; private set; }

        public void SetScanResult(CodebaseScanResult result)
        {
            _nextResult = result;
            ScanWasCalled = false;
        }

        public Task<CodebaseScanResult> ScanAsync(CodebaseScanRequest request, IProgress<CodebaseScanProgress>? progress = null, CancellationToken ct = default)
        {
            ScanWasCalled = true;
            return Task.FromResult(_nextResult ?? new CodebaseScanResult
            {
                IsSuccess = true,
                Solutions = new List<SolutionInfo>(),
                Projects = new List<ProjectInfo>()
            });
        }
    }

    private class FakeMapValidator : IMapValidator
    {
        private MapValidationResult? _nextResult;

        public void SetValidationResult(MapValidationResult result)
        {
            _nextResult = result;
        }

        public Task<MapValidationResult> ValidateAsync(MapValidationRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(_nextResult ?? new MapValidationResult
            {
                IsValid = true,
                Summary = new MapValidationSummary { ArtifactsValidated = 1, ErrorCount = 0, WarningCount = 0, InfoCount = 0 }
            });
        }
    }
}
