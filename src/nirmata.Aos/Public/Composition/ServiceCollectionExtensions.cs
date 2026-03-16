using nirmata.Aos.Public.Configuration;
using nirmata.Aos.Engine;
using nirmata.Aos.Engine.Commands;
using nirmata.Aos.Public.Models;
using nirmata.Aos.Public.Services;
using nirmata.Aos.Engine.Paths;
using nirmata.Aos.Engine.Registry;
using nirmata.Aos.Engine.Validation;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Catalogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace nirmata.Aos.Public.Composition;

/// <summary>
/// Extension methods for registering nirmata AOS services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds nirmata AOS services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration to bind AOS options from.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// Lifetime conventions:
    /// <para>
    /// <b>Singleton</b> (stores and workspace-bound services, initialized in dependency order):
    /// - <see cref="IWorkspace"/> - workspace root at repository path
    /// - <see cref="ISpecStore"/> - spec storage tied to workspace
    /// - <see cref="IStateStore"/> - state storage tied to workspace
    /// - <see cref="IEvidenceStore"/> - evidence storage tied to workspace
    /// - <see cref="IValidator"/> - workspace validation
    /// - <see cref="CommandCatalog"/> - shared command registration catalog
    /// - <see cref="IArtifactPathResolver"/> - artifact ID to canonical path mapping
    /// - <see cref="IDeterministicJsonSerializer"/> - canonical JSON serialization
    /// - <see cref="ISchemaRegistry"/> - embedded and local schema registry
    /// - <see cref="IRunManager"/> - run lifecycle management
    /// - <see cref="IEventStore"/> - event log operations
    /// - <see cref="ICheckpointManager"/> - checkpoint creation/restoration
    /// - <see cref="ILockManager"/> - workspace lock management
    /// - <see cref="ICacheManager"/> - workspace cache directory maintenance
    /// </para>
    /// <para>
    /// <b>Scoped</b> (per-invocation):
    /// - <see cref="ICommandRouter"/> - command routing isolation per invocation
    /// </para>
    /// </remarks>
    public static IServiceCollection AddnirmataAos(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<AosOptions>(configuration.GetSection("nirmataAos"));

        // Singleton: IWorkspace - the workspace is rooted at a specific repository path
        services.AddSingleton<IWorkspace>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AosOptions>>().Value;
            return Workspace.FromRepositoryRoot(options.RepositoryRootPath);
        });

        // Singleton: ISpecStore - spec store is tied to the workspace aos root
        services.AddSingleton<ISpecStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return SpecStore.FromWorkspace(workspace);
        });

        // Also register concrete SpecStore for components that depend on it directly
        services.AddSingleton<SpecStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return SpecStore.FromWorkspace(workspace);
        });

        // Singleton: IStateStore - state store is tied to the workspace aos root
        services.AddSingleton<IStateStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return StateStore.FromWorkspace(workspace);
        });

        // Singleton: IEvidenceStore - evidence store is tied to the workspace aos root
        services.AddSingleton<IEvidenceStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return EvidenceStore.FromWorkspace(workspace);
        });

        // Singleton: IValidator - workspace validator (uses static methods internally)
        services.AddSingleton<IValidator, AosWorkspaceValidatorWrapper>();

        // Singleton: CommandCatalog - shared catalog for command registration
        services.AddSingleton<CommandCatalog>(sp =>
        {
            var catalog = new CommandCatalog();
            RegisterPhaseHandlers(catalog);
            return catalog;
        });

        // Scoped: ICommandRouter - per-invocation command routing
        services.AddScoped<ICommandRouter>(sp =>
        {
            var catalog = sp.GetRequiredService<CommandCatalog>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var evidenceStore = sp.GetService<IEvidenceStore>();
            return new CommandRouter(catalog, workspace, evidenceStore);
        });

        // Singleton: IArtifactPathResolver - artifact ID to canonical path mapping
        services.AddSingleton<IArtifactPathResolver, ArtifactPathResolver>();

        // Singleton: IDeterministicJsonSerializer - canonical JSON serialization
        services.AddSingleton<IDeterministicJsonSerializer, DeterministicJsonSerializer>();

        // Singleton: ISchemaRegistry - embedded and local schema registry
        services.AddSingleton<ISchemaRegistry, SchemaRegistry>();

        // Singleton: IRunManager - run lifecycle management
        services.AddSingleton<IRunManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return RunManager.FromWorkspace(workspace);
        });

        // Singleton: IEventStore - event log operations
        services.AddSingleton<IEventStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return EventStore.FromWorkspace(workspace);
        });

        // Singleton: ICheckpointManager - checkpoint creation/restoration
        services.AddSingleton<ICheckpointManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return CheckpointManager.FromWorkspace(workspace);
        });

        // Singleton: ILockManager - workspace lock management
        services.AddSingleton<ILockManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return LockManager.FromWorkspace(workspace);
        });

        // Singleton: ICacheManager - workspace cache directory maintenance
        services.AddSingleton<ICacheManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetService<ILogger<CacheManager>>();
            return CacheManager.FromWorkspace(workspace, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds nirmata AOS services to the dependency injection container using a specific repository root path.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="repositoryRootPath">The repository root path for the AOS workspace.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddnirmataAos(
        this IServiceCollection services,
        string repositoryRootPath)
    {
        // Bind configuration from explicit path
        services.Configure<AosOptions>(options => options.RepositoryRootPath = repositoryRootPath);

        // Singleton: IWorkspace
        services.AddSingleton<IWorkspace>(_ => Workspace.FromRepositoryRoot(repositoryRootPath));

        // Singleton: ISpecStore
        services.AddSingleton<ISpecStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return SpecStore.FromWorkspace(workspace);
        });

        // Also register concrete SpecStore for components that depend on it directly
        services.AddSingleton<SpecStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return SpecStore.FromWorkspace(workspace);
        });

        // Singleton: IStateStore
        services.AddSingleton<IStateStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return StateStore.FromWorkspace(workspace);
        });

        // Singleton: IEvidenceStore
        services.AddSingleton<IEvidenceStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return EvidenceStore.FromWorkspace(workspace);
        });

        // Singleton: IValidator
        services.AddSingleton<IValidator, AosWorkspaceValidatorWrapper>();

        // Singleton: CommandCatalog - shared catalog for command registration
        services.AddSingleton<CommandCatalog>(sp =>
        {
            var catalog = new CommandCatalog();
            RegisterPhaseHandlers(catalog);
            return catalog;
        });

        // Scoped: ICommandRouter
        services.AddScoped<ICommandRouter>(sp =>
        {
            var catalog = sp.GetRequiredService<CommandCatalog>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var evidenceStore = sp.GetService<IEvidenceStore>();
            return new CommandRouter(catalog, workspace, evidenceStore);
        });

        // Singleton: IArtifactPathResolver - artifact ID to canonical path mapping
        services.AddSingleton<IArtifactPathResolver, ArtifactPathResolver>();

        // Singleton: IDeterministicJsonSerializer - canonical JSON serialization
        services.AddSingleton<IDeterministicJsonSerializer, DeterministicJsonSerializer>();

        // Singleton: ISchemaRegistry - embedded and local schema registry
        services.AddSingleton<ISchemaRegistry, SchemaRegistry>();

        // Singleton: IRunManager - run lifecycle management
        services.AddSingleton<IRunManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return RunManager.FromWorkspace(workspace);
        });

        // Singleton: IEventStore - event log operations
        services.AddSingleton<IEventStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return EventStore.FromWorkspace(workspace);
        });

        // Singleton: ICheckpointManager - checkpoint creation/restoration
        services.AddSingleton<ICheckpointManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return CheckpointManager.FromWorkspace(workspace);
        });

        // Singleton: ILockManager - workspace lock management
        services.AddSingleton<ILockManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return LockManager.FromWorkspace(workspace);
        });

        // Singleton: ICacheManager - workspace cache directory maintenance
        services.AddSingleton<ICacheManager>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetService<ILogger<CacheManager>>();
            return CacheManager.FromWorkspace(workspace, logger);
        });

        return services;
    }

    /// <summary>
    /// Registers phase handler commands in the command catalog.
    /// These commands map to the six orchestrator phases: Interviewer, Roadmapper, Planner, Executor, Verifier, FixPlanner.
    /// </summary>
    private static void RegisterPhaseHandlers(CommandCatalog catalog)
    {
        // Interviewer phase: Initialize project specification
        catalog.Register(
            new Contracts.Commands.CommandMetadata
            {
                Group = "spec",
                Command = "init",
                Id = "spec.init",
                Description = "Initialize project specification (Interviewer phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Project specification initialized"))
        );

        // Roadmapper phase: Create roadmap
        catalog.Register(
            new Contracts.Commands.CommandMetadata
            {
                Group = "spec",
                Command = "roadmap",
                Id = "spec.roadmap",
                Description = "Create project roadmap (Roadmapper phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Roadmap created"))
        );

        // Planner phase: Create execution plan
        catalog.Register(
            new Contracts.Commands.CommandMetadata
            {
                Group = "spec",
                Command = "plan",
                Id = "spec.plan",
                Description = "Create execution plan (Planner phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Execution plan created"))
        );

        // Executor phase: Execute plan tasks
        catalog.Register(
            new Contracts.Commands.CommandMetadata
            {
                Group = "run",
                Command = "execute",
                Id = "run.execute",
                Description = "Execute plan tasks (Executor phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Plan execution completed"))
        );

        // Verifier phase: Verify execution results
        catalog.Register(
            new Contracts.Commands.CommandMetadata
            {
                Group = "run",
                Command = "verify",
                Id = "run.verify",
                Description = "Verify execution results (Verifier phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Execution verified"))
        );

        // FixPlanner phase: Create fix plan for failed verification
        catalog.Register(
            new Contracts.Commands.CommandMetadata
            {
                Group = "spec",
                Command = "fix",
                Id = "spec.fix",
                Description = "Create fix plan for failed verification (FixPlanner phase)"
            },
            ctx => Task.FromResult(CommandResult.Success("Fix plan created"))
        );

        // OpenSpec validation command
        var openspecValidateHandler = new Engine.Commands.Spec.OpenspecValidateCommandHandler();
        catalog.Register(
            openspecValidateHandler.Metadata,
            ctx => openspecValidateHandler.ExecuteAsync(ctx)
        );
    }
}
