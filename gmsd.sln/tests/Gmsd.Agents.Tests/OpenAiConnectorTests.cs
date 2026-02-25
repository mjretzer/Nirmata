using FluentAssertions;
using Gmsd.Agents.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Xunit;

namespace Gmsd.Agents.Tests;

public class OpenAiConnectorTests
{
    [Fact]
    public void AddSemanticKernel_WithOpenAiProvider_RegistersKernelAndChatCompletionService()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
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
    public void AddSemanticKernel_WithOpenAiProvider_MapsExecutionSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o",
                ["GmsdAgents:SemanticKernel:OpenAi:Temperature"] = "0.7",
                ["GmsdAgents:SemanticKernel:OpenAi:MaxTokens"] = "1024",
                ["GmsdAgents:SemanticKernel:OpenAi:TopP"] = "0.9",
                ["GmsdAgents:SemanticKernel:OpenAi:FrequencyPenalty"] = "0.5",
                ["GmsdAgents:SemanticKernel:OpenAi:PresencePenalty"] = "0.3",
                ["GmsdAgents:SemanticKernel:OpenAi:Seed"] = "42"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var kernel = provider.GetRequiredService<Kernel>();
        var executionSettings = kernel.Services.GetService<OpenAIPromptExecutionSettings>();
        executionSettings.Should().NotBeNull();
        executionSettings!.Temperature.Should().Be(0.7);
        executionSettings.MaxTokens.Should().Be(1024);
        executionSettings.TopP.Should().Be(0.9);
        executionSettings.FrequencyPenalty.Should().Be(0.5);
        executionSettings.PresencePenalty.Should().Be(0.3);
        executionSettings.Seed.Should().Be(42);
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProvider_DefaultExecutionSettings()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var kernel = provider.GetRequiredService<Kernel>();
        var executionSettings = kernel.Services.GetService<OpenAIPromptExecutionSettings>();
        executionSettings.Should().NotBeNull();
        executionSettings!.Temperature.Should().Be(1.0);
        executionSettings.MaxTokens.Should().Be(2048);
        executionSettings.TopP.Should().Be(1.0);
        executionSettings.FrequencyPenalty.Should().Be(0.0);
        executionSettings.PresencePenalty.Should().Be(0.0);
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProviderAndOrganizationId_RegistersSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o",
                ["GmsdAgents:SemanticKernel:OpenAi:OrganizationId"] = "org-123"
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
    public void AddSemanticKernel_WithOpenAiProviderAndBaseUrl_RegistersSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o",
                ["GmsdAgents:SemanticKernel:OpenAi:BaseUrl"] = "https://custom.openai.api.com"
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
    public void AddSemanticKernel_WithOpenAiProvider_MissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        Action act = () =>
        {
            services.AddSemanticKernel(configuration);
            var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<Kernel>();
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OpenAI API key is required*");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProvider_MissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        Action act = () =>
        {
            services.AddSemanticKernel(configuration);
            var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<Kernel>();
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OpenAI model ID is required*");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProvider_MissingConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        Action act = () =>
        {
            services.AddSemanticKernel(configuration);
            var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<Kernel>();
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*OpenAI configuration is missing*");
    }
}
