using Gmsd.Agents.Configuration;
using Gmsd.Agents.Execution.Backlog.DeferredIssuesCurator;
using Gmsd.Agents.Execution.Backlog.TodoCapturer;
using Gmsd.Agents.Execution.Backlog.TodoReviewer;
using Gmsd.Agents.Execution.Brownfield.CodebaseScanner;
using Gmsd.Agents.Execution.Brownfield.MapValidator;
using Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder;
using Gmsd.Agents.Execution.Context;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.Validation;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Execution.Continuity;
using Gmsd.Agents.Execution.Continuity.HistoryWriter;
using Gmsd.Agents.Execution.Continuity.ProgressReporter;
using Gmsd.Agents.Execution.Verification.Issues;
using Gmsd.Agents.Execution.Verification.UatVerifier;
using Gmsd.Agents.Execution.Execution.AtomicGitCommitter;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Execution.Execution.TaskExecutor;
using Gmsd.Agents.Execution.Planning.PhasePlanner;
using Gmsd.Agents.Execution.Planning.PhasePlanner.Assumptions;
using Gmsd.Agents.Execution.Planning.PhasePlanner.ContextGatherer;
using Gmsd.Agents.Execution.Planning.RoadmapModifier;
using Gmsd.Agents.Execution.Planning;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Agents.Persistence.State;
using Gmsd.Agents.Observability;
using Gmsd.Agents.Workers;
using Gmsd.Aos.Composition;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Catalogs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using FileInfo = System.IO.FileInfo;

namespace Gmsd.Agents.Configuration;

/// <summary>
/// Extension methods for registering GMSD Agents services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds GMSD Agents services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration to bind Agents options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGmsdAgents(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register Engine services (AOS layer)
        services.AddGmsdAos(configuration);

        // Register Plane-specific services
        services.AddLlmProvider(configuration);

        // Register persistence abstractions
        services.AddSingleton<IRunRepository, RunRepository>();
        services.AddSingleton<IRunLifecycleManager>(sp =>
        {
            var runRepository = sp.GetRequiredService<IRunRepository>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var jsonSerializer = sp.GetRequiredService<IDeterministicJsonSerializer>();
            var eventStore = sp.GetRequiredService<IEventStore>();
            return new RunLifecycleManager(runRepository, workspace, jsonSerializer, eventStore);
        });

        // Register orchestrator services
        services.AddSingleton<IGatingEngine, GatingEngine>();
        services.AddSingleton<IPreflightValidator, PreflightValidator>();
        services.AddSingleton<IOutputValidator, OutputValidator>();
        services.AddSingleton<IContextPackManager, ContextPackManager>();
        services.AddSingleton<IOrchestrator, Orchestrator>();
        services.AddSingleton<InputClassifier>();
        services.AddSingleton<ChatResponder>();

        // Register NewProjectInterviewer services
        services.AddSingleton<IProjectSpecGenerator, ProjectSpecGenerator>();
        services.AddSingleton<IInterviewEvidenceWriter, InterviewEvidenceWriter>();
        services.AddSingleton<INewProjectInterviewer, NewProjectInterviewer>();
        services.AddSingleton<InterviewerHandler>();
        services.AddSingleton<ResponderHandler>();
        services.AddSingleton<ReadOnlyHandler>();

        // Register Roadmapper services (for the Roadmapper phase)
        services.AddSingleton<IRoadmapper, Roadmapper>();
        services.AddSingleton<IRoadmapGenerator, RoadmapGenerator>();
        services.AddSingleton<RoadmapperHandler>();

        // Register PhasePlanner services
        services.AddSingleton<IPhaseContextGatherer, PhaseContextGatherer>();
        services.AddSingleton<IPhasePlanner, Execution.Planning.PhasePlanner.PhasePlanner>();
        services.AddSingleton<IPhaseAssumptionLister, PhaseAssumptionLister>();
        services.AddSingleton<PhasePlannerHandler>();

        // Register RoadmapModifier services (for modifying existing roadmaps)
        services.AddSingleton<IRoadmapRenumberer, RoadmapRenumberer>();
        services.AddSingleton<IRoadmapModifier, RoadmapModifier>();
        services.AddSingleton<RoadmapModifierHandler>();
        services.AddSingleton<CursorCoherencePreserver>();
        services.AddSingleton<RoadmapValidator>();
        services.AddSingleton<AtomicSpecWriter>();
        services.AddSingleton<RoadmapModificationGate>();

        // Register observability services
        services.AddSingleton<ICorrelationIdProvider, RunCorrelationIdProvider>();

        // Register TaskExecutor services
        services.AddSingleton<ITaskExecutor>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var subagentOrchestrator = sp.GetRequiredService<ISubagentOrchestrator>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var stateStore = sp.GetRequiredService<IStateStore>();
            var logger = sp.GetRequiredService<ILogger<TaskExecutor>>();
            return new TaskExecutor(runLifecycleManager, subagentOrchestrator, workspace, stateStore, logger);
        });

        // Register TaskExecutorHandler
        services.AddSingleton<TaskExecutorHandler>();

        // Register AtomicGitCommitter services
        services.AddScoped<IAtomicGitCommitter, AtomicGitCommitter>();
        services.AddSingleton<AtomicGitCommitterHandler>();

        // Register UAT Verifier services
        services.AddSingleton<IUatCheckRunner, UatCheckRunner>();
        services.AddSingleton<IIssueWriter, IssueWriter>();
        services.AddSingleton<IUatResultWriter, UatResultWriter>();
        services.AddSingleton<IUatVerifier, UatVerifier>();
        services.AddSingleton<VerifierHandler>();

        // Register CodebaseMapper services
        services.AddSingleton<ICodebaseScanner, Execution.Brownfield.CodebaseScanner.CodebaseScanner>();
        services.AddSingleton<IMapValidator, Execution.Brownfield.MapValidator.MapValidator>();
        services.AddSingleton<ISymbolCacheBuilder, Execution.Brownfield.SymbolCacheBuilder.SymbolCacheBuilder>();
        services.AddSingleton<CodebaseMapperHandler>();

        // Register SubagentOrchestrator services
        services.AddSingleton<ISubagentOrchestrator>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetRequiredService<ILogger<SubagentOrchestrator>>();
            return new SubagentOrchestrator(runLifecycleManager, workspace, logger);
        });

        // Register Continuity services (Pause/Resume)
        services.AddSingleton<IHandoffStateStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return new HandoffStateStore(workspace.AosRootPath);
        });
        services.AddSingleton<IPauseResumeManager, PauseResumeManager>();
        services.AddSingleton<PauseWorkCommandHandler>();
        services.AddSingleton<ResumeWorkCommandHandler>();
        services.AddSingleton<ResumeTaskCommandHandler>();

        // Register Progress Reporter and History Writer services
        services.AddSingleton<IProgressReporter>(sp =>
        {
            var stateStore = sp.GetRequiredService<IStateStore>();
            return new ProgressReporter(stateStore);
        });
        services.AddSingleton<IHistoryWriter>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return new HistoryWriter(workspace.AosRootPath);
        });

        // Register Continuity Command Handlers
        services.AddSingleton<ReportProgressCommandHandler>();
        services.AddSingleton<WriteHistoryCommandHandler>();

        // Register continuity commands in CommandCatalog (replaces the AOS-registered catalog)
        services.AddSingleton<CommandCatalog>(sp =>
        {
            // Create and populate the catalog with phase handlers
            var catalog = new CommandCatalog();

            // Register phase handlers (mirroring AOS registration)
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "init",
                    Id = "spec.init",
                    Description = "Initialize project specification (Interviewer phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Project specification initialized"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "roadmap",
                    Id = "spec.roadmap",
                    Description = "Create project roadmap (Roadmapper phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Roadmap created"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "plan",
                    Id = "spec.plan",
                    Description = "Create execution plan (Planner phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Execution plan created"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "run",
                    Command = "execute",
                    Id = "run.execute",
                    Description = "Execute plan tasks (Executor phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Plan execution completed"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "run",
                    Command = "verify",
                    Id = "run.verify",
                    Description = "Verify execution results (Verifier phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Execution verified"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "fix",
                    Id = "spec.fix",
                    Description = "Create fix plan for failed verification (FixPlanner phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Fix plan created"))
            );

            // Register continuity commands
            ContinuityCommandRegistrar.Register(catalog, sp);

            // Register CodebaseMapper commands
            var codebaseMapperHandler = sp.GetRequiredService<CodebaseMapperHandler>();

            // Register map command
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "map",
                    Id = "codebase.map",
                    Description = "Map codebase structure and generate intelligence pack"
                },
                async ctx =>
                {
                    var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "map",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.HandleAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? Gmsd.Aos.Engine.Commands.Base.CommandResult.Success(result.Output)
                        : Gmsd.Aos.Engine.Commands.Base.CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            // Register validate command
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "validate",
                    Id = "codebase.validate",
                    Description = "Validate existing codebase map integrity and schema compliance"
                },
                async ctx =>
                {
                    var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "validate",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.ValidateAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? Gmsd.Aos.Engine.Commands.Base.CommandResult.Success(result.Output)
                        : Gmsd.Aos.Engine.Commands.Base.CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            // Register refresh-symbols command
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "refresh-symbols",
                    Id = "codebase.refresh-symbols",
                    Description = "Refresh symbol cache incrementally for changed files"
                },
                async ctx =>
                {
                    var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "refresh-symbols",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.RefreshSymbolsAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? Gmsd.Aos.Engine.Commands.Base.CommandResult.Success(result.Output)
                        : Gmsd.Aos.Engine.Commands.Base.CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            return catalog;
        });

        // Bind Agents-specific configuration
        services.Configure<AgentsOptions>(configuration.GetSection("GmsdAgents"));

        // Register Backlog Triage services
        services.AddSingleton<IDeferredIssuesCurator, Agents.Execution.Backlog.DeferredIssuesCurator.DeferredIssuesCurator>();
        services.AddSingleton<ITodoCapturer, Agents.Execution.Backlog.TodoCapturer.TodoCapturer>();
        services.AddSingleton<ITodoReviewer, Agents.Execution.Backlog.TodoReviewer.TodoReviewer>();

        // Register background worker services
        services.AddHostedService<AgentRuntimeWorker>();

        return services;
    }

    /// <summary>
    /// Adds GMSD Agents services to the dependency injection container using a specific repository root path.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="repositoryRootPath">The repository root path for the AOS workspace.</param>
    /// <param name="configuration">The configuration to bind Agents options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGmsdAgents(
        this IServiceCollection services,
        string repositoryRootPath,
        IConfiguration configuration)
    {
        // Register Engine services with explicit path
        services.AddGmsdAos(repositoryRootPath);

        // Register Plane-specific services
        services.AddLlmProvider(configuration);

        // Register persistence abstractions
        services.AddSingleton<IRunRepository, RunRepository>();
        services.AddSingleton<IRunLifecycleManager>(sp =>
        {
            var runRepository = sp.GetRequiredService<IRunRepository>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var jsonSerializer = sp.GetRequiredService<IDeterministicJsonSerializer>();
            var eventStore = sp.GetRequiredService<IEventStore>();
            return new RunLifecycleManager(runRepository, workspace, jsonSerializer, eventStore);
        });

        // Register orchestrator services
        services.AddSingleton<IGatingEngine, GatingEngine>();
        services.AddSingleton<IPreflightValidator, PreflightValidator>();
        services.AddSingleton<IOutputValidator, OutputValidator>();
        services.AddSingleton<IContextPackManager, ContextPackManager>();
        services.AddSingleton<IOrchestrator, Orchestrator>();
        services.AddSingleton<InputClassifier>();
        services.AddSingleton<ChatResponder>();

        // Register NewProjectInterviewer services
        services.AddSingleton<IProjectSpecGenerator, ProjectSpecGenerator>();
        services.AddSingleton<IInterviewEvidenceWriter, InterviewEvidenceWriter>();
        services.AddSingleton<INewProjectInterviewer, NewProjectInterviewer>();
        services.AddSingleton<InterviewerHandler>();
        services.AddSingleton<ResponderHandler>();
        services.AddSingleton<ReadOnlyHandler>();

        // Register Roadmapper services (for the Roadmapper phase)
        services.AddSingleton<IRoadmapper, Roadmapper>();
        services.AddSingleton<IRoadmapGenerator, RoadmapGenerator>();
        services.AddSingleton<RoadmapperHandler>();

        // Register PhasePlanner services
        services.AddSingleton<IPhaseContextGatherer, PhaseContextGatherer>();
        services.AddSingleton<IPhasePlanner, Execution.Planning.PhasePlanner.PhasePlanner>();
        services.AddSingleton<IPhaseAssumptionLister, PhaseAssumptionLister>();
        services.AddSingleton<PhasePlannerHandler>();

        // Register RoadmapModifier services (for modifying existing roadmaps)
        services.AddSingleton<IRoadmapRenumberer, RoadmapRenumberer>();
        services.AddSingleton<IRoadmapModifier, RoadmapModifier>();
        services.AddSingleton<RoadmapModifierHandler>();
        services.AddSingleton<CursorCoherencePreserver>();
        services.AddSingleton<RoadmapValidator>();
        services.AddSingleton<AtomicSpecWriter>();
        services.AddSingleton<RoadmapModificationGate>();

        // Register observability services
        services.AddSingleton<ICorrelationIdProvider, RunCorrelationIdProvider>();

        // Register TaskExecutor services
        services.AddSingleton<ITaskExecutor>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var subagentOrchestrator = sp.GetRequiredService<ISubagentOrchestrator>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var stateStore = sp.GetRequiredService<IStateStore>();
            var logger = sp.GetRequiredService<ILogger<TaskExecutor>>();
            return new TaskExecutor(runLifecycleManager, subagentOrchestrator, workspace, stateStore, logger);
        });

        // Register TaskExecutorHandler
        services.AddSingleton<TaskExecutorHandler>();

        // Register AtomicGitCommitter services
        services.AddScoped<IAtomicGitCommitter, AtomicGitCommitter>();
        services.AddSingleton<AtomicGitCommitterHandler>();

        // Register UAT Verifier services
        services.AddSingleton<IUatCheckRunner, UatCheckRunner>();
        services.AddSingleton<IIssueWriter, IssueWriter>();
        services.AddSingleton<IUatResultWriter, UatResultWriter>();
        services.AddSingleton<IUatVerifier, UatVerifier>();
        services.AddSingleton<VerifierHandler>();

        // Register CodebaseMapper services
        services.AddSingleton<ICodebaseScanner, Execution.Brownfield.CodebaseScanner.CodebaseScanner>();
        services.AddSingleton<IMapValidator, Execution.Brownfield.MapValidator.MapValidator>();
        services.AddSingleton<ISymbolCacheBuilder, Execution.Brownfield.SymbolCacheBuilder.SymbolCacheBuilder>();
        services.AddSingleton<CodebaseMapperHandler>();

        // Register SubagentOrchestrator services
        services.AddSingleton<ISubagentOrchestrator>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetRequiredService<ILogger<SubagentOrchestrator>>();
            return new SubagentOrchestrator(runLifecycleManager, workspace, logger);
        });

        // Register Continuity services (Pause/Resume)
        services.AddSingleton<IHandoffStateStore>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return new HandoffStateStore(workspace.AosRootPath);
        });
        services.AddSingleton<IPauseResumeManager, PauseResumeManager>();
        services.AddSingleton<PauseWorkCommandHandler>();
        services.AddSingleton<ResumeWorkCommandHandler>();
        services.AddSingleton<ResumeTaskCommandHandler>();

        // Register Progress Reporter and History Writer services
        services.AddSingleton<IProgressReporter>(sp =>
        {
            var stateStore = sp.GetRequiredService<IStateStore>();
            return new ProgressReporter(stateStore);
        });
        services.AddSingleton<IHistoryWriter>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            return new HistoryWriter(workspace.AosRootPath);
        });

        // Register Continuity Command Handlers
        services.AddSingleton<ReportProgressCommandHandler>();
        services.AddSingleton<WriteHistoryCommandHandler>();

        // Register continuity commands in CommandCatalog (replaces the AOS-registered catalog)
        services.AddSingleton<CommandCatalog>(sp =>
        {
            // Create and populate the catalog with phase handlers
            var catalog = new CommandCatalog();

            // Register phase handlers (mirroring AOS registration)
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "init",
                    Id = "spec.init",
                    Description = "Initialize project specification (Interviewer phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Project specification initialized"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "roadmap",
                    Id = "spec.roadmap",
                    Description = "Create project roadmap (Roadmapper phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Roadmap created"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "plan",
                    Id = "spec.plan",
                    Description = "Create execution plan (Planner phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Execution plan created"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "run",
                    Command = "execute",
                    Id = "run.execute",
                    Description = "Execute plan tasks (Executor phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Plan execution completed"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "run",
                    Command = "verify",
                    Id = "run.verify",
                    Description = "Verify execution results (Verifier phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Execution verified"))
            );
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "fix",
                    Id = "spec.fix",
                    Description = "Create fix plan for failed verification (FixPlanner phase)"
                },
                ctx => Task.FromResult(Gmsd.Aos.Engine.Commands.Base.CommandResult.Success("Fix plan created"))
            );

            // Register continuity commands
            ContinuityCommandRegistrar.Register(catalog, sp);

            // Register CodebaseMapper commands
            var codebaseMapperHandler = sp.GetRequiredService<CodebaseMapperHandler>();

            // Register map command
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "map",
                    Id = "codebase.map",
                    Description = "Map codebase structure and generate intelligence pack"
                },
                async ctx =>
                {
                    var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "map",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.HandleAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? Gmsd.Aos.Engine.Commands.Base.CommandResult.Success(result.Output)
                        : Gmsd.Aos.Engine.Commands.Base.CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            // Register validate command
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "validate",
                    Id = "codebase.validate",
                    Description = "Validate existing codebase map integrity and schema compliance"
                },
                async ctx =>
                {
                    var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "validate",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.ValidateAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? Gmsd.Aos.Engine.Commands.Base.CommandResult.Success(result.Output)
                        : Gmsd.Aos.Engine.Commands.Base.CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            // Register refresh-symbols command
            catalog.Register(
                new Gmsd.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "refresh-symbols",
                    Id = "codebase.refresh-symbols",
                    Description = "Refresh symbol cache incrementally for changed files"
                },
                async ctx =>
                {
                    var request = new Gmsd.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "refresh-symbols",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.RefreshSymbolsAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? Gmsd.Aos.Engine.Commands.Base.CommandResult.Success(result.Output)
                        : Gmsd.Aos.Engine.Commands.Base.CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            return catalog;
        });

        // Bind Agents-specific configuration
        services.Configure<AgentsOptions>(configuration.GetSection("GmsdAgents"));

        // Register Backlog Triage services
        services.AddSingleton<IDeferredIssuesCurator, Agents.Execution.Backlog.DeferredIssuesCurator.DeferredIssuesCurator>();
        services.AddSingleton<ITodoCapturer, Agents.Execution.Backlog.TodoCapturer.TodoCapturer>();
        services.AddSingleton<ITodoReviewer, Agents.Execution.Backlog.TodoReviewer.TodoReviewer>();

        // Register background worker services
        services.AddHostedService<AgentRuntimeWorker>();

        return services;
    }
}
