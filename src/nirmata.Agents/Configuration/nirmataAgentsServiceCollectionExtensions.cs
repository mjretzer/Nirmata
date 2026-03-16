using nirmata.Agents.Configuration;
using nirmata.Agents.Execution.Backlog.DeferredIssuesCurator;
using nirmata.Agents.Execution.Backlog.TodoCapturer;
using nirmata.Agents.Execution.Backlog.TodoReviewer;
using nirmata.Agents.Execution.Brownfield.CodebaseScanner;
using nirmata.Agents.Execution.Brownfield.MapValidator;
using nirmata.Agents.Execution.Brownfield.SymbolCacheBuilder;
using nirmata.Agents.Execution.Context;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.ControlPlane.Streaming;
using nirmata.Agents.Execution.ControlPlane.Chat;
using nirmata.Agents.Execution.ControlPlane.Tools.Registry;
using nirmata.Agents.Execution.ControlPlane.Tools.Standard;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Execution.Preflight;
using nirmata.Agents.Execution.Preflight.CommandSuggestion;
using nirmata.Agents.Execution.Continuity;
using nirmata.Agents.Execution.Continuity.HistoryWriter;
using nirmata.Agents.Execution.Continuity.ProgressReporter;
using nirmata.Agents.Execution.ToolCalling;
using nirmata.Agents.Execution.Verification.Issues;
using nirmata.Agents.Execution.Verification.UatVerifier;
using nirmata.Agents.Execution.Execution.AtomicGitCommitter;
using nirmata.Agents.Execution.Execution.SubagentRuns;
using nirmata.Agents.Execution.Execution.TaskExecutor;
using nirmata.Agents.Execution.LlmProvider;
using nirmata.Agents.Execution.Planning.PhasePlanner;
using nirmata.Agents.Execution.Planning.PhasePlanner.Assumptions;
using nirmata.Agents.Execution.Planning.PhasePlanner.ContextGatherer;
using nirmata.Agents.Execution.Planning.RoadmapModifier;
using nirmata.Agents.Execution.Planning;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Persistence.State;
using nirmata.Agents.Observability;
using nirmata.Agents.Workers;
using nirmata.Aos.Public.Composition;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Catalogs;
using nirmata.Aos.Public.Services;
using nirmata.Aos.Public.Models;
using nirmata.Aos.Configuration;
using nirmata.Aos.Concurrency;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using FileInfo = System.IO.FileInfo;

namespace nirmata.Agents.Configuration;

/// <summary>
/// Extension methods for registering nirmata Agents services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds nirmata Agents services to the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The configuration to bind Agents options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddnirmataAgents(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register Engine services (AOS layer)
        services.AddnirmataAos(configuration);

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
        services.AddSingleton<IGatingEngine>(sp =>
        {
            var destructivenessAnalyzer = sp.GetRequiredService<IDestructivenessAnalyzer>();
            var confirmationEvaluator = sp.GetService<IConfirmationGateEvaluator>();
            return confirmationEvaluator != null
                ? new GatingEngine(destructivenessAnalyzer, confirmationEvaluator)
                : new GatingEngine(destructivenessAnalyzer);
        });
        services.AddSingleton<IDestructivenessAnalyzer, DestructivenessAnalyzer>();
        services.AddSingleton<IConfirmationGateEvaluator, ConfirmationGateEvaluator>();
        services.AddSingleton<IConfirmationGate, ConfirmationGate>();
        services.AddSingleton<IPreflightValidator, PreflightValidator>();
        services.AddSingleton<IPrerequisiteValidator, PrerequisiteValidator>();
        services.AddSingleton<IOutputValidator, OutputValidator>();
        services.AddSingleton<IContextPackManager, ContextPackManager>();
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var gatingEngine = sp.GetRequiredService<IGatingEngine>();
            var commandRouter = sp.GetRequiredService<ICommandRouter>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var specStore = sp.GetRequiredService<ISpecStore>();
            var stateStore = sp.GetRequiredService<IStateStore>();
            var validator = sp.GetRequiredService<IValidator>();
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var confirmationGate = sp.GetRequiredService<IConfirmationGate>();
            var confirmationGateEvaluator = sp.GetService<IConfirmationGateEvaluator>();
            var interviewerHandler = sp.GetRequiredService<InterviewerHandler>();
            var roadmapperHandler = sp.GetRequiredService<RoadmapperHandler>();
            var phasePlannerHandler = sp.GetRequiredService<PhasePlannerHandler>();
            var taskExecutorHandler = sp.GetRequiredService<TaskExecutorHandler>();
            var verifierHandler = sp.GetRequiredService<VerifierHandler>();
            var atomicGitCommitterHandler = sp.GetRequiredService<AtomicGitCommitterHandler>();
            var preflightValidator = sp.GetRequiredService<IPreflightValidator>();
            var prerequisiteValidator = sp.GetRequiredService<IPrerequisiteValidator>();
            var outputValidator = sp.GetRequiredService<IOutputValidator>();
            var contextPackManager = sp.GetRequiredService<IContextPackManager>();
            var responderHandler = sp.GetRequiredService<ResponderHandler>();
            var inputClassifier = sp.GetRequiredService<InputClassifier>();
            var chatResponder = sp.GetRequiredService<ChatResponder>();
            var readOnlyHandler = sp.GetRequiredService<ReadOnlyHandler>();
            var confirmationEventPublisher = sp.GetService<ConfirmationEventPublisher>();

            if (confirmationGateEvaluator != null)
            {
                return new Orchestrator(
                    gatingEngine,
                    commandRouter,
                    workspace,
                    specStore,
                    stateStore,
                    validator,
                    runLifecycleManager,
                    confirmationGate,
                    confirmationGateEvaluator,
                    interviewerHandler,
                    roadmapperHandler,
                    phasePlannerHandler,
                    taskExecutorHandler,
                    verifierHandler,
                    atomicGitCommitterHandler,
                    preflightValidator,
                    prerequisiteValidator,
                    outputValidator,
                    contextPackManager,
                    responderHandler,
                    inputClassifier,
                    chatResponder,
                    readOnlyHandler,
                    confirmationEventPublisher);
            }
            else
            {
                return new Orchestrator(
                    gatingEngine,
                    commandRouter,
                    workspace,
                    specStore,
                    stateStore,
                    validator,
                    runLifecycleManager,
                    confirmationGate,
                    interviewerHandler,
                    roadmapperHandler,
                    phasePlannerHandler,
                    taskExecutorHandler,
                    verifierHandler,
                    atomicGitCommitterHandler,
                    preflightValidator,
                    prerequisiteValidator,
                    outputValidator,
                    contextPackManager,
                    responderHandler,
                    inputClassifier,
                    chatResponder,
                    readOnlyHandler,
                    confirmationEventPublisher);
            }
        });
        services.AddSingleton<InputClassifier>();

        // Register command suggestion services
        services.Configure<CommandSuggestionOptions>(configuration.GetSection("nirmataAgents:CommandSuggestion"));
        services.AddSingleton<ICommandSuggester, LlmCommandSuggester>();

        services.AddSingleton<ChatResponder>();

        // Register Chat Responder services
        services.AddSingleton<ChatPromptBuilder>();
        services.AddSingleton<IChatContextAssembly, ChatContextAssembly>();
        services.AddSingleton<IChatResponder, LlmChatResponder>();

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

        // Register Concurrency Limiter services (Task 4.1-4.8)
        services.AddSingleton<IConcurrencyLimiter>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetRequiredService<ILogger<ConcurrencyLimiter>>();
            var configLoader = new ConcurrencyConfigurationLoader(workspace, sp.GetRequiredService<ILogger<ConcurrencyConfigurationLoader>>());
            var options = configLoader.Load();
            return new ConcurrencyLimiter(options, logger);
        });

        // Register TaskExecutor services with concurrency limiting
        services.AddSingleton<ITaskExecutor>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var toolCallingLoop = sp.GetRequiredService<IToolCallingLoop>();
            var toolRegistry = sp.GetRequiredService<IToolRegistry>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var stateStore = sp.GetRequiredService<IStateStore>();
            var logger = sp.GetRequiredService<ILogger<TaskExecutor>>();
            var innerExecutor = new TaskExecutor(runLifecycleManager, toolCallingLoop, toolRegistry, workspace, stateStore, logger);
            var concurrencyLimiter = sp.GetRequiredService<IConcurrencyLimiter>();
            var limitedLogger = sp.GetRequiredService<ILogger<ConcurrencyLimiterTaskExecutor>>();
            return new ConcurrencyLimiterTaskExecutor(innerExecutor, concurrencyLimiter, limitedLogger);
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

        // Register Standard Tools (Task 3.1-3.3)
        services.RegisterTool<FileReadTool>();
        services.RegisterTool<FileWriteTool>();
        services.RegisterTool<ProcessRunnerTool>();
        services.RegisterTool<GitTool>();
        services.RegisterTool<BuildTool>();
        services.RegisterTool<TestTool>();

        // Initialize Tool Registry (Task 3.4)
        services.InitializeToolRegistry();

        // Register Tool Calling services (Task 6.1)
        services.AddSingleton<IToolCallingLoop>(sp =>
        {
            var chatCompletionService = sp.GetRequiredService<IChatCompletionService>();
            var toolRegistry = sp.GetRequiredService<IToolRegistry>();
            var eventEmitter = sp.GetService<IToolCallingEventEmitter>();
            return new ToolCallingLoop(chatCompletionService, toolRegistry, eventEmitter);
        });

        // Register SubagentOrchestrator services
        services.AddSingleton<ISubagentOrchestrator>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var toolCallingLoop = sp.GetRequiredService<IToolCallingLoop>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetRequiredService<ILogger<SubagentOrchestrator>>();
            return new SubagentOrchestrator(runLifecycleManager, toolCallingLoop, workspace, logger);
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
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "init",
                    Id = "spec.init",
                    Description = "Initialize project specification (Interviewer phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Project specification initialized"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "roadmap",
                    Id = "spec.roadmap",
                    Description = "Create project roadmap (Roadmapper phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Roadmap created"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "plan",
                    Id = "spec.plan",
                    Description = "Create execution plan (Planner phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Execution plan created"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "run",
                    Command = "execute",
                    Id = "run.execute",
                    Description = "Execute plan tasks (Executor phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Plan execution completed"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "run",
                    Command = "verify",
                    Id = "run.verify",
                    Description = "Verify execution results (Verifier phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Execution verified"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "fix",
                    Id = "spec.fix",
                    Description = "Create fix plan for failed verification (FixPlanner phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Fix plan created"))
            );

            // Register continuity commands
            ContinuityCommandRegistrar.Register(catalog, sp);

            // Register CodebaseMapper commands
            var codebaseMapperHandler = sp.GetRequiredService<CodebaseMapperHandler>();

            // Register map command
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "map",
                    Id = "codebase.map",
                    Description = "Map codebase structure and generate intelligence pack"
                },
                async ctx =>
                {
                    var request = new nirmata.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "map",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.HandleAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? CommandResult.Success(result.Output)
                        : CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            // Register validate command
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "validate",
                    Id = "codebase.validate",
                    Description = "Validate existing codebase map integrity and schema compliance"
                },
                async ctx =>
                {
                    var request = new nirmata.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "validate",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.ValidateAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? CommandResult.Success(result.Output)
                        : CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            // Register refresh-symbols command
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "refresh-symbols",
                    Id = "codebase.refresh-symbols",
                    Description = "Refresh symbol cache incrementally for changed files"
                },
                async ctx =>
                {
                    var request = new nirmata.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "refresh-symbols",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.RefreshSymbolsAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? CommandResult.Success(result.Output)
                        : CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            return catalog;
        });

        // Bind Agents-specific configuration
        services.Configure<AgentsOptions>(configuration.GetSection(AgentsOptions.SectionName));
        services.Configure<ConfirmationOptions>(configuration.GetSection(ConfirmationOptions.SectionName));

        // Register Backlog Triage services
        services.AddSingleton<IDeferredIssuesCurator, Agents.Execution.Backlog.DeferredIssuesCurator.DeferredIssuesCurator>();
        services.AddSingleton<ITodoCapturer, Agents.Execution.Backlog.TodoCapturer.TodoCapturer>();
        services.AddSingleton<ITodoReviewer, Agents.Execution.Backlog.TodoReviewer.TodoReviewer>();

        // Register background worker services
        services.AddHostedService<AgentRuntimeWorker>();

        return services;
    }

    /// <summary>
    /// Adds nirmata Agents services to the dependency injection container using a specific repository root path.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="repositoryRootPath">The repository root path for the AOS workspace.</param>
    /// <param name="configuration">The configuration to bind Agents options from.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddnirmataAgents(
        this IServiceCollection services,
        string repositoryRootPath,
        IConfiguration configuration)
    {
        // Register Engine services with explicit path
        services.AddnirmataAos(repositoryRootPath);

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
        services.AddSingleton<IGatingEngine>(sp =>
        {
            var destructivenessAnalyzer = sp.GetRequiredService<IDestructivenessAnalyzer>();
            var confirmationEvaluator = sp.GetService<IConfirmationGateEvaluator>();
            return confirmationEvaluator != null
                ? new GatingEngine(destructivenessAnalyzer, confirmationEvaluator)
                : new GatingEngine(destructivenessAnalyzer);
        });
        services.AddSingleton<IDestructivenessAnalyzer, DestructivenessAnalyzer>();
        services.AddSingleton<IConfirmationGateEvaluator, ConfirmationGateEvaluator>();
        services.AddSingleton<IConfirmationGate, ConfirmationGate>();
        services.AddSingleton<IPreflightValidator, PreflightValidator>();
        services.AddSingleton<IPrerequisiteValidator, PrerequisiteValidator>();
        services.AddSingleton<IOutputValidator, OutputValidator>();
        services.AddSingleton<IContextPackManager, ContextPackManager>();
        services.AddSingleton<IOrchestrator>(sp =>
        {
            var gatingEngine = sp.GetRequiredService<IGatingEngine>();
            var commandRouter = sp.GetRequiredService<ICommandRouter>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var specStore = sp.GetRequiredService<ISpecStore>();
            var stateStore = sp.GetRequiredService<IStateStore>();
            var validator = sp.GetRequiredService<IValidator>();
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var confirmationGate = sp.GetRequiredService<IConfirmationGate>();
            var confirmationGateEvaluator = sp.GetService<IConfirmationGateEvaluator>();
            var interviewerHandler = sp.GetRequiredService<InterviewerHandler>();
            var roadmapperHandler = sp.GetRequiredService<RoadmapperHandler>();
            var phasePlannerHandler = sp.GetRequiredService<PhasePlannerHandler>();
            var taskExecutorHandler = sp.GetRequiredService<TaskExecutorHandler>();
            var verifierHandler = sp.GetRequiredService<VerifierHandler>();
            var atomicGitCommitterHandler = sp.GetRequiredService<AtomicGitCommitterHandler>();
            var preflightValidator = sp.GetRequiredService<IPreflightValidator>();
            var prerequisiteValidator = sp.GetRequiredService<IPrerequisiteValidator>();
            var outputValidator = sp.GetRequiredService<IOutputValidator>();
            var contextPackManager = sp.GetRequiredService<IContextPackManager>();
            var responderHandler = sp.GetRequiredService<ResponderHandler>();
            var inputClassifier = sp.GetRequiredService<InputClassifier>();
            var chatResponder = sp.GetRequiredService<ChatResponder>();
            var readOnlyHandler = sp.GetRequiredService<ReadOnlyHandler>();
            var confirmationEventPublisher = sp.GetService<ConfirmationEventPublisher>();

            if (confirmationGateEvaluator != null)
            {
                return new Orchestrator(
                    gatingEngine,
                    commandRouter,
                    workspace,
                    specStore,
                    stateStore,
                    validator,
                    runLifecycleManager,
                    confirmationGate,
                    confirmationGateEvaluator,
                    interviewerHandler,
                    roadmapperHandler,
                    phasePlannerHandler,
                    taskExecutorHandler,
                    verifierHandler,
                    atomicGitCommitterHandler,
                    preflightValidator,
                    prerequisiteValidator,
                    outputValidator,
                    contextPackManager,
                    responderHandler,
                    inputClassifier,
                    chatResponder,
                    readOnlyHandler,
                    confirmationEventPublisher);
            }
            else
            {
                return new Orchestrator(
                    gatingEngine,
                    commandRouter,
                    workspace,
                    specStore,
                    stateStore,
                    validator,
                    runLifecycleManager,
                    confirmationGate,
                    interviewerHandler,
                    roadmapperHandler,
                    phasePlannerHandler,
                    taskExecutorHandler,
                    verifierHandler,
                    atomicGitCommitterHandler,
                    preflightValidator,
                    prerequisiteValidator,
                    outputValidator,
                    contextPackManager,
                    responderHandler,
                    inputClassifier,
                    chatResponder,
                    readOnlyHandler,
                    confirmationEventPublisher);
            }
        });
        services.AddSingleton<InputClassifier>();

        // Register command suggestion services
        services.Configure<CommandSuggestionOptions>(configuration.GetSection("nirmataAgents:CommandSuggestion"));
        services.AddSingleton<ICommandSuggester, LlmCommandSuggester>();

        services.AddSingleton<ChatResponder>();

        // Register Chat Responder services
        services.AddSingleton<ChatPromptBuilder>();
        services.AddSingleton<IChatContextAssembly, ChatContextAssembly>();
        services.AddSingleton<IChatResponder, LlmChatResponder>();

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

        // Register Concurrency Limiter services (Task 4.1-4.8)
        services.AddSingleton<IConcurrencyLimiter>(sp =>
        {
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetRequiredService<ILogger<ConcurrencyLimiter>>();
            var configLoader = new ConcurrencyConfigurationLoader(workspace, sp.GetRequiredService<ILogger<ConcurrencyConfigurationLoader>>());
            var options = configLoader.Load();
            return new ConcurrencyLimiter(options, logger);
        });

        // Register TaskExecutor services with concurrency limiting
        services.AddSingleton<ITaskExecutor>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var toolCallingLoop = sp.GetRequiredService<IToolCallingLoop>();
            var toolRegistry = sp.GetRequiredService<IToolRegistry>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var stateStore = sp.GetRequiredService<IStateStore>();
            var logger = sp.GetRequiredService<ILogger<TaskExecutor>>();
            var innerExecutor = new TaskExecutor(runLifecycleManager, toolCallingLoop, toolRegistry, workspace, stateStore, logger);
            var concurrencyLimiter = sp.GetRequiredService<IConcurrencyLimiter>();
            var limitedLogger = sp.GetRequiredService<ILogger<ConcurrencyLimiterTaskExecutor>>();
            return new ConcurrencyLimiterTaskExecutor(innerExecutor, concurrencyLimiter, limitedLogger);
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

        // Register Standard Tools (Task 3.1-3.3)
        services.RegisterTool<FileReadTool>();
        services.RegisterTool<FileWriteTool>();
        services.RegisterTool<ProcessRunnerTool>();
        services.RegisterTool<GitTool>();
        services.RegisterTool<BuildTool>();
        services.RegisterTool<TestTool>();

        // Initialize Tool Registry (Task 3.4)
        services.InitializeToolRegistry();

        // Register Tool Calling services (Task 6.1)
        services.AddSingleton<IToolCallingLoop>(sp =>
        {
            var chatCompletionService = sp.GetRequiredService<IChatCompletionService>();
            var toolRegistry = sp.GetRequiredService<IToolRegistry>();
            var eventEmitter = sp.GetService<IToolCallingEventEmitter>();
            return new ToolCallingLoop(chatCompletionService, toolRegistry, eventEmitter);
        });

        // Register SubagentOrchestrator services
        services.AddSingleton<ISubagentOrchestrator>(sp =>
        {
            var runLifecycleManager = sp.GetRequiredService<IRunLifecycleManager>();
            var toolCallingLoop = sp.GetRequiredService<IToolCallingLoop>();
            var workspace = sp.GetRequiredService<IWorkspace>();
            var logger = sp.GetRequiredService<ILogger<SubagentOrchestrator>>();
            return new SubagentOrchestrator(runLifecycleManager, toolCallingLoop, workspace, logger);
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
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "init",
                    Id = "spec.init",
                    Description = "Initialize project specification (Interviewer phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Project specification initialized"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "roadmap",
                    Id = "spec.roadmap",
                    Description = "Create project roadmap (Roadmapper phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Roadmap created"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "plan",
                    Id = "spec.plan",
                    Description = "Create execution plan (Planner phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Execution plan created"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "run",
                    Command = "execute",
                    Id = "run.execute",
                    Description = "Execute plan tasks (Executor phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Plan execution completed"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "run",
                    Command = "verify",
                    Id = "run.verify",
                    Description = "Verify execution results (Verifier phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Execution verified"))
            );
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "spec",
                    Command = "fix",
                    Id = "spec.fix",
                    Description = "Create fix plan for failed verification (FixPlanner phase)"
                },
                ctx => Task.FromResult(CommandResult.Success("Fix plan created"))
            );

            // Register continuity commands
            ContinuityCommandRegistrar.Register(catalog, sp);

            // Register CodebaseMapper commands
            var codebaseMapperHandler = sp.GetRequiredService<CodebaseMapperHandler>();

            // Register map command
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "map",
                    Id = "codebase.map",
                    Description = "Map codebase structure and generate intelligence pack"
                },
                async ctx =>
                {
                    var request = new nirmata.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "map",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.HandleAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? CommandResult.Success(result.Output)
                        : CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            // Register validate command
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "validate",
                    Id = "codebase.validate",
                    Description = "Validate existing codebase map integrity and schema compliance"
                },
                async ctx =>
                {
                    var request = new nirmata.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "validate",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.ValidateAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? CommandResult.Success(result.Output)
                        : CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            // Register refresh-symbols command
            catalog.Register(
                new nirmata.Aos.Contracts.Commands.CommandMetadata
                {
                    Group = "codebase",
                    Command = "refresh-symbols",
                    Id = "codebase.refresh-symbols",
                    Description = "Refresh symbol cache incrementally for changed files"
                },
                async ctx =>
                {
                    var request = new nirmata.Aos.Contracts.Commands.CommandRequest
                    {
                        Group = "codebase",
                        Command = "refresh-symbols",
                        Arguments = ctx.Arguments.ToArray(),
                        Options = ctx.Options.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    };
                    var result = await codebaseMapperHandler.RefreshSymbolsAsync(request, "unknown", ctx.CancellationToken);
                    return result.IsSuccess
                        ? CommandResult.Success(result.Output)
                        : CommandResult.Failure(result.ExitCode, result.ErrorOutput, result.Errors);
                }
            );

            return catalog;
        });

        // Bind Agents-specific configuration
        services.Configure<AgentsOptions>(configuration.GetSection(AgentsOptions.SectionName));
        services.Configure<ConfirmationOptions>(configuration.GetSection(ConfirmationOptions.SectionName));

        // Register Backlog Triage services
        services.AddSingleton<IDeferredIssuesCurator, Agents.Execution.Backlog.DeferredIssuesCurator.DeferredIssuesCurator>();
        services.AddSingleton<ITodoCapturer, Agents.Execution.Backlog.TodoCapturer.TodoCapturer>();
        services.AddSingleton<ITodoReviewer, Agents.Execution.Backlog.TodoReviewer.TodoReviewer>();

        // Register background worker services
        services.AddHostedService<AgentRuntimeWorker>();

        return services;
    }
}
