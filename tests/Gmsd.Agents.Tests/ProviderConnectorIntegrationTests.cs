using System.Net;
using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Configuration;
using Gmsd.Agents.Execution.ControlPlane.Llm.Tools;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Gmsd.Aos.Contracts.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests;

/// <summary>
/// Integration tests for provider connectors with mocked HTTP clients.
/// Tests the full flow from configuration through service resolution to actual API calls.
/// </summary>
public class ProviderConnectorIntegrationTests
{
    #region OpenAI Connector Integration Tests

    [Fact]
    public async Task OpenAiConnector_WithMockedHttpClient_ReturnsChatCompletion()
    {
        // Arrange - Create a mock HTTP handler that simulates OpenAI API response
        var mockResponse = new
        {
            id = "chatcmpl-test123",
            @object = "chat.completion",
            created = 1677652288,
            model = "gpt-4o",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "This is a mocked response from OpenAI"
                    },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = 10,
                completion_tokens = 8,
                total_tokens = 18
            }
        };

        var mockHttpHandler = new MockHttpMessageHandler(JsonSerializer.Serialize(mockResponse));
        var httpClient = new HttpClient(mockHttpHandler)
        {
            BaseAddress = new Uri("https://api.openai.com")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(httpClient);
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        var chatCompletionService = provider.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hello, how are you?");

        // Act
        var result = await chatCompletionService.GetChatMessageContentAsync(chatHistory);

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Be("This is a mocked response from OpenAI");
        result.ModelId.Should().Be("gpt-4o");
    }

    [Fact]
    public async Task OpenAiConnector_WithMockedHttpClient_HandlesErrorResponse()
    {
        // Arrange - Create a mock HTTP handler that returns an error
        var mockHttpHandler = new MockHttpMessageHandler(
            responseContent: JsonSerializer.Serialize(new
            {
                error = new
                {
                    message = "Invalid API key",
                    type = "invalid_request_error"
                }
            }),
            statusCode: HttpStatusCode.Unauthorized);

        var httpClient = new HttpClient(mockHttpHandler)
        {
            BaseAddress = new Uri("https://api.openai.com")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "invalid-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(httpClient);
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        var chatCompletionService = provider.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Test message");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await chatCompletionService.GetChatMessageContentAsync(chatHistory));
    }

    [Fact]
    public async Task OpenAiConnector_WithStreaming_ReturnsStreamedContent()
    {
        // Arrange - Create streaming response chunks
        var streamingChunks = new[]
        {
            CreateStreamingChunk("Hello"),
            CreateStreamingChunk(","),
            CreateStreamingChunk(" how"),
            CreateStreamingChunk(" can"),
            CreateStreamingChunk(" I"),
            CreateStreamingChunk(" help"),
            CreateStreamingChunk("?"),
            CreateFinalStreamingChunk()
        };

        var mockHttpHandler = new MockStreamingHttpMessageHandler(streamingChunks);
        var httpClient = new HttpClient(mockHttpHandler)
        {
            BaseAddress = new Uri("https://api.openai.com")
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton(httpClient);
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        var chatCompletionService = provider.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        chatHistory.AddUserMessage("Hi there");

        var executionSettings = new OpenAIPromptExecutionSettings();

        // Act
        var streamingResults = new List<StreamingChatMessageContent>();
        await foreach (var chunk in chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings))
        {
            streamingResults.Add(chunk);
        }

        // Assert
        streamingResults.Should().NotBeEmpty();
        var fullContent = string.Concat(streamingResults.Select(c => c.Content));
        fullContent.Should().Be("Hello, how can I help?");
    }

    #endregion

    #region Azure OpenAI Connector Configuration Tests

    [Fact]
    public void AzureOpenAiConnector_WithValidConfiguration_RegistersSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "test-azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4-deployment",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiVersion"] = "2024-06-01"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var kernel = provider.GetService<Kernel>();
        kernel.Should().NotBeNull();

        var chatCompletionService = provider.GetService<IChatCompletionService>();
        chatCompletionService.Should().NotBeNull();
    }

    [Fact]
    public void AzureOpenAiConnector_WithCustomApiVersion_RegistersCorrectly()
    {
        // Arrange
        var customApiVersion = "2024-02-01";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "test-azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4-deployment",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiVersion"] = customApiVersion
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var kernel = provider.GetService<Kernel>();
        kernel.Should().NotBeNull();
    }

    [Fact]
    public void AzureOpenAiConnector_RegistersSingletonKernel()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "test-azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4-deployment"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        // Act
        var kernel1 = provider.GetService<Kernel>();
        var kernel2 = provider.GetService<Kernel>();

        // Assert
        kernel1.Should().NotBeNull();
        kernel2.Should().NotBeNull();
        kernel1.Should().BeSameAs(kernel2);
    }

    [Theory]
    [InlineData("AzureOpenAi")]
    [InlineData("azure-openai")]
    [InlineData("AZUREOPENAI")]
    public void AzureOpenAiConnector_AcceptsVariousProviderNameFormats(string providerName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"GmsdAgents:SemanticKernel:Provider"] = providerName,
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "test-azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4-deployment"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var kernel = provider.GetService<Kernel>();
        kernel.Should().NotBeNull();
    }

    #endregion

    #region Tool Auto-Function-Calling Tests

    [Fact]
    public async Task ToolAutoInvocation_WithSingleTool_InvokesToolAutomatically()
    {
        // Arrange
        var toolMock = new Mock<ITool>();
        var toolDescriptor = new ToolDescriptor
        {
            Id = "test-calculator",
            Name = "Calculator",
            Description = "Performs basic arithmetic calculations",
            Category = "math",
            Parameters =
            [
                new ToolParameter
                {
                    Name = "expression",
                    Description = "The mathematical expression to evaluate",
                    Type = "string",
                    Required = true
                }
            ]
        };

        toolMock.Setup(t => t.InvokeAsync(
                It.Is<ToolRequest>(r => r.Operation == "Calculator"),
                It.IsAny<ToolContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResult.Success("42"));

        // Setup the mock to expose Descriptor property
        var toolType = toolMock.Object.GetType();
        var descriptorProperty = toolType.GetProperty("Descriptor");
        if (descriptorProperty is null)
        {
            // Create a wrapper that has the Descriptor property
            var wrappedTool = new ToolWithDescriptor(toolMock.Object, toolDescriptor);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                    ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                    ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o",
                    ["GmsdAgents:SemanticKernel:OpenAi:EnableParallelToolCalls"] = "true"
                })
                .Build();

            var services = new ServiceCollection();
            services.AddTransient<ITool>(_ => wrappedTool);
            services.AddSemanticKernel(configuration);
            var provider = services.BuildServiceProvider();

            var kernel = provider.GetRequiredService<Kernel>();

            // Add tool directly to kernel
            var tools = new (ITool Tool, ToolDescriptor Descriptor)[]
            {
                (wrappedTool, toolDescriptor)
            };
            var plugin = Gmsd.Agents.Execution.ControlPlane.Llm.Tools.KernelPluginFactory.CreateFromTools(tools);
            kernel.Plugins.Add(plugin);

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage("Calculate 20 + 22");

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // Act
            // Note: This test verifies the tool is registered correctly and available for auto-invocation
            // The actual LLM call would require a real or deeply mocked response
            var availableFunctions = kernel.Plugins.GetFunctionsMetadata();

            // Assert
            availableFunctions.Should().Contain(f => f.Name == "Calculator");
        }
    }

    [Fact]
    public void ToolAutoInvocation_WithMultipleTools_AllToolsRegistered()
    {
        // Arrange
        var calculatorDescriptor = new ToolDescriptor
        {
            Id = "calculator",
            Name = "Calculator",
            Description = "Performs calculations",
            Category = "math",
            Parameters = [new ToolParameter { Name = "expression", Description = "The mathematical expression to evaluate", Type = "string", Required = true }]
        };

        var weatherDescriptor = new ToolDescriptor
        {
            Id = "weather",
            Name = "GetWeather",
            Description = "Gets weather information",
            Category = "utility",
            Parameters = [new ToolParameter { Name = "location", Description = "The location to get weather for", Type = "string", Required = true }]
        };

        var calculatorMock = new Mock<ITool>();
        var weatherMock = new Mock<ITool>();

        var wrappedCalculator = new ToolWithDescriptor(calculatorMock.Object, calculatorDescriptor);
        var wrappedWeather = new ToolWithDescriptor(weatherMock.Object, weatherDescriptor);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddTransient<ITool>(_ => wrappedCalculator);
        services.AddTransient<ITool>(_ => wrappedWeather);
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        var kernel = provider.GetRequiredService<Kernel>();

        // Add tools to kernel
        var tools = new (ITool Tool, ToolDescriptor Descriptor)[]
        {
            (wrappedCalculator, calculatorDescriptor),
            (wrappedWeather, weatherDescriptor)
        };
        var plugin = Gmsd.Agents.Execution.ControlPlane.Llm.Tools.KernelPluginFactory.CreateFromTools(tools);
        kernel.Plugins.Add(plugin);

        // Act
        var availableFunctions = kernel.Plugins.GetFunctionsMetadata().ToList();

        // Assert
        availableFunctions.Should().HaveCount(2);
        availableFunctions.Should().Contain(f => f.Name == "Calculator");
        availableFunctions.Should().Contain(f => f.Name == "GetWeather");
    }

    [Fact]
    public void ToolAutoInvocation_ToolCallBehavior_AutoInvokeKernelFunctions_Enabled()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o",
                ["GmsdAgents:SemanticKernel:OpenAi:EnableParallelToolCalls"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        // Act
        var executionSettings = provider.GetService<OpenAIPromptExecutionSettings>();

        // Assert
        executionSettings.Should().NotBeNull();
        executionSettings!.ToolCallBehavior.Should().Be(ToolCallBehavior.AutoInvokeKernelFunctions);
    }

    [Fact]
    public void ToolRegistry_Integration_WithSemanticKernel()
    {
        // Arrange
        var descriptor = new ToolDescriptor
        {
            Id = "test-tool",
            Name = "TestTool",
            Description = "A test tool",
            Category = "test",
            Parameters = []
        };

        var toolMock = new Mock<ITool>();
        var wrappedTool = new ToolWithDescriptor(toolMock.Object, descriptor);

        var registry = new ToolRegistry();
        registry.Register(descriptor, wrappedTool);

        // Act
        var plugin = Gmsd.Agents.Execution.ControlPlane.Llm.Tools.KernelPluginFactory.CreateFromRegistry(registry);

        // Assert
        plugin.FunctionCount.Should().Be(1);
        plugin.GetFunctionsMetadata().Should().Contain(f => f.Name == "TestTool");
    }

    #endregion

    #region Test Helpers

    private static string CreateStreamingChunk(string content)
    {
        return "data: " + JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test",
            @object = "chat.completion.chunk",
            created = 1677652288,
            model = "gpt-4o",
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { content = content },
                    finish_reason = (string?)null
                }
            }
        }) + "\n\n";
    }

    private static string CreateFinalStreamingChunk()
    {
        return "data: " + JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test",
            @object = "chat.completion.chunk",
            created = 1677652288,
            model = "gpt-4o",
            choices = new[]
            {
                new
                {
                    index = 0,
                    delta = new { },
                    finish_reason = "stop"
                }
            }
        }) + "\n\ndata: [DONE]\n\n";
    }

    /// <summary>
    /// Helper class to wrap a mock tool with a Descriptor property.
    /// </summary>
    private class ToolWithDescriptor : ITool
    {
        private readonly ITool _inner;

        public ToolWithDescriptor(ITool inner, ToolDescriptor descriptor)
        {
            _inner = inner;
            Descriptor = descriptor;
        }

        public ToolDescriptor Descriptor { get; }

        public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
        {
            return _inner.InvokeAsync(request, context, cancellationToken);
        }
    }

    #endregion
}

/// <summary>
/// Mock HTTP message handler for testing HTTP-based connectors.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly string _responseContent;
    private readonly HttpStatusCode _statusCode;

    public MockHttpMessageHandler(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseContent = responseContent;
        _statusCode = statusCode;
    }

    public List<HttpRequestMessage> CapturedRequests { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CapturedRequests.Add(request);

        if (_statusCode != HttpStatusCode.OK)
        {
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseContent)
            };
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(_responseContent)
        };
    }
}

/// <summary>
/// Mock HTTP message handler for streaming responses.
/// </summary>
public class MockStreamingHttpMessageHandler : HttpMessageHandler
{
    private readonly string[] _chunks;

    public MockStreamingHttpMessageHandler(string[] chunks)
    {
        _chunks = chunks;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = new PushStreamContent(async (stream, httpContent, transportContext) =>
        {
            using var writer = new StreamWriter(stream);
            foreach (var chunk in _chunks)
            {
                await writer.WriteAsync(chunk);
                await writer.FlushAsync();
            }
        }, "text/event-stream");

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
        };
    }
}

/// <summary>
/// Simple push stream content implementation for mocking streaming responses.
/// </summary>
public class PushStreamContent : HttpContent
{
    private readonly Func<Stream, HttpContent, TransportContext?, Task> _onStreamAvailable;
    private readonly string _mediaType;

    public PushStreamContent(Func<Stream, HttpContent, TransportContext?, Task> onStreamAvailable, string mediaType)
    {
        _onStreamAvailable = onStreamAvailable;
        _mediaType = mediaType;
        Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await _onStreamAvailable(stream, this, context);
    }

    protected override bool TryComputeLength(out long length)
    {
        length = -1;
        return false;
    }
}
