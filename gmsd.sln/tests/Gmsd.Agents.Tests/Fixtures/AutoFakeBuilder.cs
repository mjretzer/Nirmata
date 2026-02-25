using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.Planning;
using Gmsd.Agents.Execution.Planning.PhasePlanner;
using Gmsd.Agents.Execution.Planning.RoadmapModifier;
using Gmsd.Agents.Execution.Brownfield.CodebaseScanner;
using Gmsd.Agents.Execution.Brownfield.MapValidator;
using Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder;
using Gmsd.Agents.Execution.FixPlanner;
using Gmsd.Agents.Execution.Execution.TaskExecutor;
using Gmsd.Agents.Execution.Verification.UatVerifier;
using Gmsd.Agents.Execution.Verification.Issues;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Agents.Persistence.State;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Gmsd.Agents.Tests.Fixtures;

/// <summary>
/// Builder for creating default mocks for all 7 orchestrator handlers.
/// Provides a fluent API for configuring custom mock behavior.
/// </summary>
public sealed class AutoFakeBuilder
{
    private readonly Mock<AtomicGitCommitterHandler> _atomicGitCommitterHandlerMock;
    private readonly Mock<CodebaseMapperHandler> _codebaseMapperHandlerMock;
    private readonly Mock<FixPlannerHandler> _fixPlannerHandlerMock;
    private readonly Mock<InterviewerHandler> _interviewerHandlerMock;
    private readonly Mock<PhasePlannerHandler> _phasePlannerHandlerMock;
    private readonly Mock<RoadmapModifierHandler> _roadmapModifierHandlerMock;
    private readonly Mock<TaskExecutorHandler> _taskExecutorHandlerMock;
    private readonly Mock<VerifierHandler> _verifierHandlerMock;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoFakeBuilder"/> class.
    /// Creates default mocks for all 7 orchestrator handlers with successful responses.
    /// </summary>
    public AutoFakeBuilder()
    {
        // Initialize all handler mocks with default successful behavior
        _atomicGitCommitterHandlerMock = new Mock<AtomicGitCommitterHandler>(
            Mock.Of<global::Gmsd.Agents.Execution.Execution.AtomicGitCommitter.IAtomicGitCommitter>(),
            Mock.Of<IWorkspace>(),
            Mock.Of<IStateStore>());
        _atomicGitCommitterHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Atomic git commit completed"));

        _codebaseMapperHandlerMock = new Mock<CodebaseMapperHandler>(
            Mock.Of<global::Gmsd.Agents.Execution.Brownfield.CodebaseScanner.ICodebaseScanner>(),
            Mock.Of<global::Gmsd.Agents.Execution.Brownfield.MapValidator.IMapValidator>(),
            Mock.Of<global::Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder.ISymbolCacheBuilder>(),
            Mock.Of<IWorkspace>(),
            Mock.Of<IRunLifecycleManager>());
        _codebaseMapperHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Codebase mapped"));
        _codebaseMapperHandlerMock
            .Setup(h => h.ValidateAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Codebase validated"));
        _codebaseMapperHandlerMock
            .Setup(h => h.RefreshSymbolsAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Symbols refreshed"));

        _fixPlannerHandlerMock = new Mock<FixPlannerHandler>(
            Mock.Of<global::Gmsd.Agents.Execution.FixPlanner.IFixPlanner>(),
            Mock.Of<IWorkspace>(),
            Mock.Of<IStateStore>(),
            Mock.Of<IRunLifecycleManager>(),
            Mock.Of<ILogger<FixPlannerHandler>>());
        _fixPlannerHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Fix plan created", routingHint: "TaskExecutor"));

        _interviewerHandlerMock = new Mock<InterviewerHandler>(
            Mock.Of<INewProjectInterviewer>(),
            Mock.Of<IWorkspace>(),
            Mock.Of<ISpecStore>());
        _interviewerHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Interview completed", routingHint: "Roadmapper"));

        _phasePlannerHandlerMock = new Mock<PhasePlannerHandler>(
            Mock.Of<IPhasePlanner>(),
            Mock.Of<IWorkspace>(),
            Mock.Of<IStateStore>(),
            Mock.Of<ISpecStore>());
        _phasePlannerHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Phase planned", routingHint: "TaskExecutor"));

        _roadmapModifierHandlerMock = new Mock<RoadmapModifierHandler>(
            Mock.Of<IRoadmapModifier>(),
            Mock.Of<IWorkspace>(),
            Mock.Of<IStateStore>(),
            Mock.Of<ISpecStore>());
        _roadmapModifierHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Roadmap modified", routingHint: "PhasePlanner"));

        _taskExecutorHandlerMock = new Mock<TaskExecutorHandler>(
            Mock.Of<global::Gmsd.Agents.Execution.Execution.TaskExecutor.ITaskExecutor>(),
            Mock.Of<IWorkspace>(),
            Mock.Of<IStateStore>(),
            Mock.Of<ISpecStore>(),
            Mock.Of<IRunLifecycleManager>());
        _taskExecutorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Tasks executed", routingHint: "Verifier"));

        _verifierHandlerMock = new Mock<VerifierHandler>(
            Mock.Of<global::Gmsd.Agents.Execution.Verification.UatVerifier.IUatVerifier>(),
            Mock.Of<global::Gmsd.Agents.Execution.Verification.Issues.IIssueWriter>(),
            Mock.Of<IWorkspace>(),
            Mock.Of<IStateStore>(),
            Mock.Of<IRunLifecycleManager>());
        _verifierHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => CreateSuccessResult("Verification completed", routingHint: "Complete"));
    }

    /// <summary>
    /// Gets the AtomicGitCommitterHandler mock for custom configuration.
    /// </summary>
    public Mock<AtomicGitCommitterHandler> AtomicGitCommitterHandler => _atomicGitCommitterHandlerMock;

    /// <summary>
    /// Gets the CodebaseMapperHandler mock for custom configuration.
    /// </summary>
    public Mock<CodebaseMapperHandler> CodebaseMapperHandler => _codebaseMapperHandlerMock;

    /// <summary>
    /// Gets the FixPlannerHandler mock for custom configuration.
    /// </summary>
    public Mock<FixPlannerHandler> FixPlannerHandler => _fixPlannerHandlerMock;

    /// <summary>
    /// Gets the InterviewerHandler mock for custom configuration.
    /// </summary>
    public Mock<InterviewerHandler> InterviewerHandler => _interviewerHandlerMock;

    /// <summary>
    /// Gets the PhasePlannerHandler mock for custom configuration.
    /// </summary>
    public Mock<PhasePlannerHandler> PhasePlannerHandler => _phasePlannerHandlerMock;

    /// <summary>
    /// Gets the RoadmapModifierHandler mock for custom configuration.
    /// </summary>
    public Mock<RoadmapModifierHandler> RoadmapModifierHandler => _roadmapModifierHandlerMock;

    /// <summary>
    /// Gets the TaskExecutorHandler mock for custom configuration.
    /// </summary>
    public Mock<TaskExecutorHandler> TaskExecutorHandler => _taskExecutorHandlerMock;

    /// <summary>
    /// Gets the VerifierHandler mock for custom configuration.
    /// </summary>
    public Mock<VerifierHandler> VerifierHandler => _verifierHandlerMock;

    /// <summary>
    /// Builds a HandlerTestHost with all handler mocks registered.
    /// </summary>
    /// <param name="repositoryRootPath">Optional repository root path.</param>
    /// <returns>A configured HandlerTestHost.</returns>
    public HandlerTestHost Build(string? repositoryRootPath = null)
    {
        var host = new HandlerTestHost(repositoryRootPath);

        // Override all handler registrations with mocks
        host.OverrideWithInstance(_atomicGitCommitterHandlerMock.Object);
        host.OverrideWithInstance(_codebaseMapperHandlerMock.Object);
        host.OverrideWithInstance(_fixPlannerHandlerMock.Object);
        host.OverrideWithInstance(_interviewerHandlerMock.Object);
        host.OverrideWithInstance(_phasePlannerHandlerMock.Object);
        host.OverrideWithInstance(_roadmapModifierHandlerMock.Object);
        host.OverrideWithInstance(_taskExecutorHandlerMock.Object);
        host.OverrideWithInstance(_verifierHandlerMock.Object);

        return host;
    }

    /// <summary>
    /// Configures the AtomicGitCommitterHandler to return a specific result.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithAtomicGitCommitterResult(CommandRouteResult result)
    {
        _atomicGitCommitterHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return this;
    }

    /// <summary>
    /// Configures the FixPlannerHandler to return a specific result.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithFixPlannerResult(CommandRouteResult result)
    {
        _fixPlannerHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return this;
    }

    /// <summary>
    /// Configures the InterviewerHandler to return a specific result.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithInterviewerResult(CommandRouteResult result)
    {
        _interviewerHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return this;
    }

    /// <summary>
    /// Configures the PhasePlannerHandler to return a specific result.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithPhasePlannerResult(CommandRouteResult result)
    {
        _phasePlannerHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return this;
    }

    /// <summary>
    /// Configures the RoadmapModifierHandler to return a specific result.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithRoadmapModifierResult(CommandRouteResult result)
    {
        _roadmapModifierHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return this;
    }

    /// <summary>
    /// Configures the TaskExecutorHandler to return a specific result.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithTaskExecutorResult(CommandRouteResult result)
    {
        _taskExecutorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return this;
    }

    /// <summary>
    /// Configures the VerifierHandler to return a specific result.
    /// </summary>
    /// <param name="result">The result to return.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithVerifierResult(CommandRouteResult result)
    {
        _verifierHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return this;
    }

    /// <summary>
    /// Configures the VerifierHandler to return a failure result.
    /// Useful for testing failure scenarios.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithVerificationFailure(string errorMessage)
    {
        _verifierHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFailureResult(errorMessage, routingHint: "FixPlanner"));
        return this;
    }

    /// <summary>
    /// Configures the TaskExecutorHandler to return a failure result.
    /// Useful for testing failure scenarios.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>The builder for chaining.</returns>
    public AutoFakeBuilder WithExecutionFailure(string errorMessage)
    {
        _taskExecutorHandlerMock
            .Setup(h => h.HandleAsync(It.IsAny<CommandRequest>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateFailureResult(errorMessage));
        return this;
    }

    private static CommandRouteResult CreateSuccessResult(string output, string? routingHint = null)
    {
        return new CommandRouteResult
        {
            IsSuccess = true,
            ExitCode = 0,
            Output = output,
            RoutingHint = routingHint,
            ErrorOutput = null,
            Errors = Array.Empty<CommandError>()
        };
    }

    private static CommandRouteResult CreateFailureResult(string errorMessage, string? routingHint = null)
    {
        return new CommandRouteResult
        {
            IsSuccess = false,
            ExitCode = 1,
            Output = string.Empty,
            RoutingHint = routingHint,
            ErrorOutput = errorMessage,
            Errors = new[] { new CommandError("HANDLER_ERROR", errorMessage) }
        };
    }
}
