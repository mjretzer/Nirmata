using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Preflight;
using Gmsd.Agents.Execution.Preflight.CommandSuggestion;
using Gmsd.Agents.Execution.Execution.AtomicGitCommitter;
using Gmsd.Web.Models.Streaming;
using Microsoft.Extensions.Options;

namespace Gmsd.Web.Tests.E2E;

/// <summary>
/// Factory for creating a test web application for E2E tests.
/// Configures mocked services for testing without external dependencies.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private sealed class NullCommandSuggester : ICommandSuggester
    {
        public Task<CommandProposal?> SuggestAsync(string input, CancellationToken cancellationToken = default)
            => Task.FromResult<CommandProposal?>(null);
    }

    /// <summary>
    /// Creates a client with mocked services properly configured.
    /// Use this instead of the base CreateClient to ensure mocks are registered.
    /// </summary>
    public HttpClient CreateClientWithMocks()
    {
        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Register missing ICommandRegistry (needed by ChatContextAssembly)
                var commandRegistryDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICommandRegistry));
                if (commandRegistryDescriptor == null)
                {
                    services.AddSingleton<ICommandRegistry, CommandRegistry>();
                }

                // Fix scoped service issue: register IAtomicGitCommitter as singleton for tests
                var atomicGitDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IAtomicGitCommitter));
                if (atomicGitDescriptor != null) services.Remove(atomicGitDescriptor);
                services.AddSingleton<IAtomicGitCommitter>(sp => new Mock<IAtomicGitCommitter>().Object);

                // Mock ILlmProvider
                var llmProviderMock = new Mock<ILlmProvider>();
                llmProviderMock.Setup(p => p.ProviderName).Returns("test");
                llmProviderMock.Setup(p => p.CompleteAsync(It.IsAny<LlmCompletionRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new LlmCompletionResponse
                    {
                        Message = LlmMessage.Assistant("Test LLM response"),
                        Model = "test-model",
                        Provider = "test"
                    });

                // Mock IOrchestrator with proper artifacts for streaming events
                var orchestratorMock = new Mock<IOrchestrator>();
                orchestratorMock.Setup(o => o.ExecuteAsync(It.IsAny<WorkflowIntent>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((WorkflowIntent intent, CancellationToken ct) => new OrchestratorResult
                    {
                        IsSuccess = true,
                        FinalPhase = "Responder",
                        RunId = "test-run-123",
                        Artifacts = new Dictionary<string, object>
                        {
                            ["response"] = $"Hello! I received your message: '{intent.InputRaw}'",
                            ["reason"] = "Input classified as chat intent"
                        }
                    });

                // Remove existing registrations
                var llmDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ILlmProvider));
                if (llmDescriptor != null) services.Remove(llmDescriptor);

                var orchDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IOrchestrator));
                if (orchDescriptor != null) services.Remove(orchDescriptor);

                services.AddSingleton<ILlmProvider>(llmProviderMock.Object);
                services.AddSingleton<IOrchestrator>(orchestratorMock.Object);

                // Re-register IStreamingOrchestrator with the mocked dependencies
                var streamingOrchDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IStreamingOrchestrator));
                if (streamingOrchDescriptor != null) services.Remove(streamingOrchDescriptor);

                services.AddSingleton<IStreamingOrchestrator>(sp =>
                {
                    var orchestrator = sp.GetRequiredService<IOrchestrator>();
                    var gatingEngine = sp.GetRequiredService<IGatingEngine>();
                    var inputClassifier = sp.GetRequiredService<InputClassifier>();
                    var llmProvider = sp.GetRequiredService<ILlmProvider>();
                    return new StreamingOrchestrator(
                        orchestrator,
                        gatingEngine,
                        inputClassifier,
                        llmProvider,
                        new NullCommandSuggester(),
                        Options.Create(new CommandSuggestionOptions { EnableSuggestionMode = false }));
                });
            });
        }).CreateClient();
    }
}
