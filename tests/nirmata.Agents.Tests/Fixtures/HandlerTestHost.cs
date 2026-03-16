using nirmata.Agents.Execution.Backlog.DeferredIssuesCurator;
using nirmata.Agents.Execution.Backlog.TodoCapturer;
using nirmata.Agents.Execution.Backlog.TodoReviewer;
using nirmata.Agents.Execution.Brownfield.CodebaseScanner;
using nirmata.Agents.Execution.Brownfield.MapValidator;
using nirmata.Agents.Execution.Brownfield.SymbolCacheBuilder;
using nirmata.Agents.Execution.Context;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.ControlPlane.Chat;
using nirmata.Agents.Execution.ControlPlane.Streaming;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Execution.Execution;
using nirmata.Agents.Execution.Execution.AtomicGitCommitter;
using nirmata.Agents.Execution.Execution.SubagentRuns;
using nirmata.Agents.Execution.Execution.TaskExecutor;
using nirmata.Agents.Execution.Planning;
using nirmata.Agents.Execution.Planning.PhasePlanner;
using nirmata.Agents.Execution.Planning.PhasePlanner.Assumptions;
using nirmata.Agents.Execution.Planning.PhasePlanner.ContextGatherer;
using nirmata.Agents.Execution.Planning.RoadmapModifier;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Execution.Verification;
using nirmata.Agents.Execution.Verification.Issues;
using nirmata.Agents.Execution.Verification.UatVerifier;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Tests.Fakes;
using nirmata.Aos.Public.Configuration;
using nirmata.Aos.Public.Templates.Prompts;
using nirmata.Aos.Engine;
using nirmata.Aos.Engine.Commands;
using nirmata.Aos.Engine.Paths;
using nirmata.Aos.Engine.Registry;
using nirmata.Aos.Engine.Stores;
using nirmata.Aos.Engine.Validation;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Catalogs;
using nirmata.Aos.Public.Services;
using nirmata.Aos.Public.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace nirmata.Agents.Tests.Fixtures;

/// <summary>
/// Test host for handler integration tests.
/// Provides a DI container with all AOS services registered using fakes by default.
/// Allows overriding specific services with mocks for isolated testing.
/// </summary>
public sealed class HandlerTestHost : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private readonly IServiceCollection _services;
    private readonly string _workspacePath;
    private bool _disposed;
    private bool _providerBuilt;

    public HandlerTestHost(string? repositoryRootPath = null)
    {
        _services = new ServiceCollection();
        _disposed = false;
        _providerBuilt = false;

        _workspacePath = repositoryRootPath ?? CreateTempWorkspace();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["nirmataAos:RepositoryRootPath"] = _workspacePath
            })
            .Build();

        _services.AddSingleton<IConfiguration>(configuration);
        _services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        _services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        RegisterAosServicesWithFakes(_workspacePath);
        RegisterHandlers();
    }

    /// <summary>
    /// Gets the service provider for resolving services.
    /// </summary>
    public IServiceProvider Services
    {
        get
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HandlerTestHost));

            if (!_providerBuilt)
            {
                _serviceProvider = _services.BuildServiceProvider();
                _providerBuilt = true;
            }

            return _serviceProvider!;
        }
    }

    /// <summary>
    /// Gets a required service from the DI container.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance.</returns>
    public T GetRequiredService<T>() where T : notnull
    {
        try
        {
            return Services.GetRequiredService<T>();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[HandlerTestHost] Failed to resolve service {typeof(T).FullName}: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.Error.WriteLine($"[HandlerTestHost] Inner exception: {ex.InnerException.Message}");
            }
            throw new InvalidOperationException($"Failed to resolve service {typeof(T).FullName}", ex);
        }
    }

    /// <summary>
    /// Gets an optional service from the DI container.
    /// </summary>
    /// <typeparam name="T">The service type.</typeparam>
    /// <returns>The service instance, or null if not registered.</returns>
    public T? GetService<T>()
        => Services.GetService<T>();

    /// <summary>
    /// Creates a new scope for resolving scoped services.
    /// </summary>
    /// <returns>A new service scope.</returns>
    public IServiceScope CreateScope()
        => Services.CreateScope();

    /// <summary>
    /// Overrides a service registration with a custom implementation.
    /// Must be called before building the service provider.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type.</typeparam>
    /// <typeparam name="TImplementation">The implementation type.</typeparam>
    /// <returns>The service collection for chaining.</returns>
    public HandlerTestHost Override<TInterface, TImplementation>()
        where TInterface : class
        where TImplementation : class, TInterface
    {
        EnsureMutable();

        // Remove existing registration
        var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(TInterface));
        if (descriptor != null)
        {
            _services.Remove(descriptor);
        }

        // Add new registration
        _services.AddSingleton<TInterface, TImplementation>();
        return this;
    }

    /// <summary>
    /// Overrides a service registration with a specific instance.
    /// Must be called before building the service provider.
    /// </summary>
    /// <typeparam name="TInterface">The service interface type.</typeparam>
    /// <param name="instance">The instance to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public HandlerTestHost OverrideWithInstance<TInterface>(TInterface instance)
        where TInterface : class
    {
        EnsureMutable();

        // Remove existing registration
        var descriptor = _services.FirstOrDefault(d => d.ServiceType == typeof(TInterface));
        if (descriptor != null)
        {
            _services.Remove(descriptor);
        }

        // Add instance registration
        _services.AddSingleton(instance);
        return this;
    }

    private void EnsureMutable()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HandlerTestHost));

        if (_providerBuilt)
        {
            _serviceProvider?.Dispose();
            _serviceProvider = null;
            _providerBuilt = false;
        }
    }

    /// <summary>
    /// Disposes the test host and all services.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        if (_providerBuilt && _serviceProvider != null)
        {
            _serviceProvider.Dispose();
        }
    }

    private static string CreateTempWorkspace()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"nirmata-test-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);
        Directory.CreateDirectory(Path.Combine(tempPath, ".aos"));
        Directory.CreateDirectory(Path.Combine(tempPath, ".aos", "state"));
        Directory.CreateDirectory(Path.Combine(tempPath, ".aos", "evidence"));
        return tempPath;
    }

    private void RegisterAosServicesWithFakes(string workspacePath)
    {
        // Bind configuration
        _services.Configure<nirmata.Aos.Public.Configuration.AosOptions>(options => options.RepositoryRootPath = workspacePath);

        // Register IWorkspace with fake
        _services.AddSingleton<IWorkspace>(_ => new FakeWorkspaceWrapper(workspacePath));

        // Register core AOS stores with fakes
        _services.AddSingleton<IStateStore>(_ => new FakeStateStore(workspacePath));
        _services.AddSingleton<IEventStore, FakeEventStore>();
        _services.AddSingleton<ISpecStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return SpecStore.FromWorkspace(workspace);
        });
        _services.AddSingleton<IPromptTemplateLoader>(sp =>
        {
            var assembly = typeof(nirmata.Agents.Configuration.ServiceCollectionExtensions).Assembly;
            return new EmbeddedResourcePromptLoader(assembly, "nirmata.Agents.Prompts");
        });

        // Register other AOS services
        _services.AddSingleton<IValidator, AosWorkspaceValidatorWrapper>();
        _services.AddSingleton<CommandCatalog>(sp =>
        {
            var catalog = new CommandCatalog();
            RegisterPhaseHandlers(catalog);
            return catalog;
        });
        _services.AddScoped<ICommandRouter>(sp =>
        {
            var catalog = sp.GetRequiredService<CommandCatalog>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var evidenceStore = sp.GetService<IEvidenceStore>();
            return new CommandRouter(catalog, workspace, evidenceStore);
        });
        _services.AddSingleton<IArtifactPathResolver, ArtifactPathResolver>();
        _services.AddSingleton<IDeterministicJsonSerializer, FakeDeterministicJsonSerializer>();
        _services.AddSingleton<ISchemaRegistry, SchemaRegistry>();
        _services.AddSingleton<IRunManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return RunManager.FromWorkspace(workspace);
        });
        _services.AddSingleton<ICheckpointManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return CheckpointManager.FromWorkspace(workspace);
        });
        _services.AddSingleton<ILockManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return LockManager.FromWorkspace(workspace);
        });
        _services.AddSingleton<ICacheManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return CacheManager.FromWorkspace(workspace);
        });

        // Register PhasePlanner services (real implementations, only need IWorkspace)
        _services.AddSingleton<IPhaseContextGatherer, PhaseContextGatherer>();
        _services.AddSingleton<IPhasePlanner, nirmata.Agents.Execution.Planning.PhasePlanner.PhasePlanner>();
        _services.AddSingleton<IPhaseAssumptionLister, PhaseAssumptionLister>();

        // Register RoadmapModifier services (real implementations)
        _services.AddSingleton<IRoadmapRenumberer, RoadmapRenumberer>();
        _services.AddSingleton<IRoadmapModifier, RoadmapModifier>();
        _services.AddSingleton<CursorCoherencePreserver>();
        _services.AddSingleton<RoadmapValidator>();
        _services.AddSingleton<AtomicSpecWriter>();
        _services.AddSingleton<RoadmapModificationGate>();

        // Register TaskExecutor services
        _services.AddSingleton<ITaskExecutor, TaskExecutor>();
        _services.AddSingleton<ISubagentOrchestrator, FakeSubagentOrchestrator>();

        // Register Verifier services
        _services.AddSingleton<IUatCheckRunner, UatCheckRunner>();
        _services.AddSingleton<IIssueWriter, IssueWriter>();
        _services.AddSingleton<IUatResultWriter, UatResultWriter>();
        _services.AddSingleton<IUatVerifier, UatVerifier>();

        // Register agent-level services with fakes
        _services.AddSingleton<IRunLifecycleManager>(_ => new FakeRunLifecycleManager(workspacePath));
        _services.AddSingleton<ILlmProvider, FakeLlmProvider>();
        _services.AddSingleton<IAtomicGitCommitter, FakeAtomicGitCommitter>();
        _services.AddSingleton<ICodebaseScanner, FakeCodebaseScanner>();
        _services.AddSingleton<ISymbolCacheBuilder, FakeSymbolCacheBuilder>();
        _services.AddSingleton<IMapValidator, FakeMapValidator>();
        _services.AddSingleton<INewProjectInterviewer, FakeNewProjectInterviewer>();
        _services.AddSingleton<IInterviewEvidenceWriter, FakeInterviewEvidenceWriter>();
        _services.AddSingleton<IRoadmapper, FakeRoadmapper>();
        _services.AddSingleton<IRoadmapGenerator, FakeRoadmapGenerator>();
        _services.AddSingleton<IToolCallingLoop, FakeToolCallingLoop>();
        _services.AddSingleton<IChatResponder, FakeChatResponder>();

        // Register orchestrator services
        _services.AddSingleton<IDestructivenessAnalyzer, DestructivenessAnalyzer>();
        _services.AddSingleton<IGatingEngine>(sp =>
        {
            var destructivenessAnalyzer = sp.GetRequiredService<IDestructivenessAnalyzer>();
            var confirmationEvaluator = sp.GetService<IConfirmationGateEvaluator>();
            return confirmationEvaluator != null
                ? new GatingEngine(destructivenessAnalyzer, confirmationEvaluator)
                : new GatingEngine(destructivenessAnalyzer);
        });
        _services.AddSingleton<IConfirmationGate, FakeConfirmationGate>();
        _services.AddSingleton<IPreflightValidator, PreflightValidator>();
        _services.AddSingleton<IPrerequisiteValidator, PrerequisiteValidator>();
        _services.AddSingleton<IOutputValidator, OutputValidator>();
        _services.AddSingleton<IOptions<ConfirmationOptions>>(_ => Options.Create(new ConfirmationOptions()));
        _services.AddSingleton<IConfirmationGateEvaluator, ConfirmationGateEvaluator>();
        _services.AddSingleton<IStreamingEventEmitter, FakeStreamingEventEmitter>();
        _services.AddSingleton<ConfirmationEventPublisher>();
        _services.AddSingleton<IContextPackManager, ContextPackManager>();
        _services.AddSingleton<InputClassifier>();
        _services.AddSingleton<ChatResponder>();
        _services.AddSingleton<IOrchestrator, Orchestrator>();
    }

    private void RegisterHandlers()
    {
        // Register all orchestrator handlers (matches Orchestrator constructor)
        _services.AddSingleton<AtomicGitCommitterHandler>();
        _services.AddSingleton<CodebaseMapperHandler>();
        _services.AddSingleton<FixPlannerHandler>();
        _services.AddSingleton<InterviewerHandler>();
        _services.AddSingleton<PhasePlannerHandler>();
        _services.AddSingleton<ReadOnlyHandler>();
        _services.AddSingleton<ResponderHandler>();
        _services.AddSingleton<RoadmapperHandler>();
        _services.AddSingleton<RoadmapModifierHandler>();
        _services.AddSingleton<TaskExecutorHandler>();
        _services.AddSingleton<VerifierHandler>();
    }

    private static void RegisterPhaseHandlers(CommandCatalog catalog)
    {
        catalog.Register(
            new Aos.Contracts.Commands.CommandMetadata
            {
                Group = "spec",
                Command = "init",
                Id = "spec.init",
                Description = "Initialize project specification (Interviewer phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Project specification initialized"))
        );

        catalog.Register(
            new Aos.Contracts.Commands.CommandMetadata
            {
                Group = "spec",
                Command = "roadmap",
                Id = "spec.roadmap",
                Description = "Create project roadmap (Roadmapper phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Roadmap created"))
        );

        catalog.Register(
            new Aos.Contracts.Commands.CommandMetadata
            {
                Group = "spec",
                Command = "plan",
                Id = "spec.plan",
                Description = "Create execution plan (Planner phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Execution plan created"))
        );

        catalog.Register(
            new Aos.Contracts.Commands.CommandMetadata
            {
                Group = "run",
                Command = "execute",
                Id = "run.execute",
                Description = "Execute plan tasks (Executor phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Plan execution completed"))
        );

        catalog.Register(
            new Aos.Contracts.Commands.CommandMetadata
            {
                Group = "run",
                Command = "verify",
                Id = "run.verify",
                Description = "Verify execution results (Verifier phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Execution verified"))
        );

        catalog.Register(
            new Aos.Contracts.Commands.CommandMetadata
            {
                Group = "spec",
                Command = "fix",
                Id = "spec.fix",
                Description = "Create fix plan for failed verification (FixPlanner phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Fix plan created"))
        );
    }

    /// <summary>
    /// Wrapper that adapts FakeWorkspace to be used with IWorkspace interface
    /// while allowing the HandlerTestHost to control the temp directory lifecycle.
    /// </summary>
    private sealed class FakeWorkspaceWrapper : IWorkspace, IDisposable
    {
        private readonly FakeWorkspace _fakeWorkspace;

        public FakeWorkspaceWrapper(string repositoryRootPath)
        {
            // Manually set up the fake workspace structure
            var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
            Directory.CreateDirectory(aosRootPath);
            Directory.CreateDirectory(Path.Combine(aosRootPath, "state"));
            Directory.CreateDirectory(Path.Combine(aosRootPath, "evidence"));

            RepositoryRootPath = repositoryRootPath;
            AosRootPath = aosRootPath;

            // Create a FakeWorkspace that uses our paths
            _fakeWorkspace = new FakeWorkspace();
        }

        public string RepositoryRootPath { get; }

        public string AosRootPath { get; }

        public string GetContractPathForArtifactId(string artifactId)
        {
            return artifactId switch
            {
                "project" => ".aos/spec/project.json",
                "roadmap" => ".aos/spec/roadmap.json",
                "plan" => ".aos/spec/plan.json",
                _ => $".aos/{artifactId}.json"
            };
        }

        public string GetAbsolutePathForContractPath(string contractPath)
        {
            return System.IO.Path.Combine(RepositoryRootPath, contractPath);
        }

        public string GetAbsolutePathForArtifactId(string artifactId)
        {
            return artifactId switch
            {
                "project" => System.IO.Path.Combine(AosRootPath, "spec", "project.json"),
                "roadmap" => System.IO.Path.Combine(AosRootPath, "spec", "roadmap.json"),
                "plan" => System.IO.Path.Combine(AosRootPath, "spec", "plan.json"),
                _ => System.IO.Path.Combine(AosRootPath, $"{artifactId}.json")
            };
        }

        public System.Text.Json.JsonElement ReadArtifact(string subpath, string filename)
        {
            return _fakeWorkspace.ReadArtifact(subpath, filename);
        }

        public void Dispose()
        {
            _fakeWorkspace.Dispose();
        }
    }

    /// <summary>
    /// Simple fake implementation of IMapValidator.
    /// </summary>
    private sealed class FakeMapValidator : IMapValidator
    {
        public Task<MapValidationResult> ValidateAsync(MapValidationRequest request, CancellationToken ct = default)
        {
            return Task.FromResult(new MapValidationResult
            {
                IsValid = true,
                Issues = Array.Empty<MapValidationIssue>()
            });
        }
    }
}
