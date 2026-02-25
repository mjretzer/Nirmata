using FluentAssertions;
using Gmsd.Agents.Configuration;
using Gmsd.Agents.Execution.ControlPlane.Tools.Contracts;
using Gmsd.Agents.Execution.ControlPlane.Tools.Registry;
using Gmsd.Aos.Contracts.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace Gmsd.Agents.Tests.Configuration;

public class SemanticKernelServiceCollectionExtensionsTests
{
    #region Configuration Binding Tests

    [Theory]
    [InlineData("OpenAi")]
    [InlineData("openai")]
    [InlineData("OPENAI")]
    public void AddSemanticKernel_WithOpenAiProvider_BindsConfigurationCorrectly(string providerName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = providerName,
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-api-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o",
                ["GmsdAgents:SemanticKernel:OpenAi:Temperature"] = "0.7",
                ["GmsdAgents:SemanticKernel:OpenAi:MaxTokens"] = "4096",
                ["GmsdAgents:SemanticKernel:OpenAi:TopP"] = "0.9",
                ["GmsdAgents:SemanticKernel:OpenAi:FrequencyPenalty"] = "0.5",
                ["GmsdAgents:SemanticKernel:OpenAi:PresencePenalty"] = "0.3",
                ["GmsdAgents:SemanticKernel:OpenAi:Seed"] = "42",
                ["GmsdAgents:SemanticKernel:OpenAi:EnableParallelToolCalls"] = "true",
                ["GmsdAgents:SemanticKernel:OpenAi:OrganizationId"] = "test-org",
                ["GmsdAgents:SemanticKernel:OpenAi:BaseUrl"] = "https://custom.openai.com"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SemanticKernelOptions>>().Value;

        // Assert
        options.Provider.Should().Be(providerName);
        options.OpenAi.Should().NotBeNull();
        options.OpenAi!.ApiKey.Should().Be("test-api-key");
        options.OpenAi.ModelId.Should().Be("gpt-4o");
        options.OpenAi.Temperature.Should().Be(0.7);
        options.OpenAi.MaxTokens.Should().Be(4096);
        options.OpenAi.TopP.Should().Be(0.9);
        options.OpenAi.FrequencyPenalty.Should().Be(0.5);
        options.OpenAi.PresencePenalty.Should().Be(0.3);
        options.OpenAi.Seed.Should().Be(42);
        options.OpenAi.EnableParallelToolCalls.Should().BeTrue();
        options.OpenAi.OrganizationId.Should().Be("test-org");
        options.OpenAi.BaseUrl.Should().Be("https://custom.openai.com");
    }

    [Theory]
    [InlineData("AzureOpenAi")]
    [InlineData("azureopenai")]
    [InlineData("azure-openai")]
    public void AddSemanticKernel_WithAzureOpenAiProvider_BindsConfigurationCorrectly(string providerName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = providerName,
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "test-azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4-deployment",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiVersion"] = "2024-06-01"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SemanticKernelOptions>>().Value;

        // Assert
        options.Provider.ToLowerInvariant().Should().BeOneOf("azureopenai", "azure-openai");
        options.AzureOpenAi.Should().NotBeNull();
        options.AzureOpenAi!.Endpoint.Should().Be("https://test.openai.azure.com");
        options.AzureOpenAi.ApiKey.Should().Be("test-azure-key");
        options.AzureOpenAi.DeploymentName.Should().Be("gpt-4-deployment");
        options.AzureOpenAi.ApiVersion.Should().Be("2024-06-01");
    }

    [Theory]
    [InlineData("Ollama")]
    [InlineData("ollama")]
    [InlineData("OLLAMA")]
    public void AddSemanticKernel_WithOllamaProvider_BindsConfigurationCorrectly(string providerName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = providerName,
                ["GmsdAgents:SemanticKernel:Ollama:BaseUrl"] = "http://localhost:11434",
                ["GmsdAgents:SemanticKernel:Ollama:ModelId"] = "llama3"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SemanticKernelOptions>>().Value;

        // Assert
        options.Provider.Should().Be(providerName);
        options.Ollama.Should().NotBeNull();
        options.Ollama!.BaseUrl.Should().Be("http://localhost:11434");
        options.Ollama.ModelId.Should().Be("llama3");
    }

    [Theory]
    [InlineData("Anthropic")]
    [InlineData("anthropic")]
    [InlineData("ANTHROPIC")]
    public void AddSemanticKernel_WithAnthropicProvider_BindsConfigurationCorrectly(string providerName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = providerName,
                ["GmsdAgents:SemanticKernel:Anthropic:ApiKey"] = "test-anthropic-key",
                ["GmsdAgents:SemanticKernel:Anthropic:ModelId"] = "claude-3-opus-20240229",
                ["GmsdAgents:SemanticKernel:Anthropic:BaseUrl"] = "https://api.anthropic.com",
                ["GmsdAgents:SemanticKernel:Anthropic:ApiVersion"] = "2023-06-01"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SemanticKernelOptions>>().Value;

        // Assert
        options.Provider.Should().Be(providerName);
        options.Anthropic.Should().NotBeNull();
        options.Anthropic!.ApiKey.Should().Be("test-anthropic-key");
        options.Anthropic.ModelId.Should().Be("claude-3-opus-20240229");
        options.Anthropic.BaseUrl.Should().Be("https://api.anthropic.com");
        options.Anthropic.ApiVersion.Should().Be("2023-06-01");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProvider_UsesDefaultValues()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SemanticKernelOptions>>().Value;

        // Assert - Default values from OpenAiSemanticKernelOptions
        options.OpenAi!.Temperature.Should().Be(1.0);
        options.OpenAi.MaxTokens.Should().Be(2048);
        options.OpenAi.TopP.Should().Be(1.0);
        options.OpenAi.FrequencyPenalty.Should().Be(0.0);
        options.OpenAi.PresencePenalty.Should().Be(0.0);
        options.OpenAi.EnableParallelToolCalls.Should().BeTrue();
    }

    [Fact]
    public void AddSemanticKernel_WithOllamaProvider_UsesDefaultValues()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Ollama",
                ["GmsdAgents:SemanticKernel:Ollama:ModelId"] = "mistral"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SemanticKernelOptions>>().Value;

        // Assert - Default values from OllamaSemanticKernelOptions
        options.Ollama!.BaseUrl.Should().Be("http://localhost:11434");
        options.Ollama.ModelId.Should().Be("mistral");
    }

    [Fact]
    public void AddSemanticKernel_WithAnthropicProvider_UsesDefaultValues()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic",
                ["GmsdAgents:SemanticKernel:Anthropic:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:Anthropic:ModelId"] = "claude-3-sonnet-20240229"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<SemanticKernelOptions>>().Value;

        // Assert - Default values from AnthropicSemanticKernelOptions
        options.Anthropic!.ApiVersion.Should().Be("2023-06-01");
    }

    #endregion

    #region Configuration Validation at DI Registration Time Tests

    [Fact]
    public void AddSemanticKernel_WithMissingSemanticKernelSection_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Semantic Kernel configuration is missing");
        exception.Message.Should().Contain("GmsdAgents:SemanticKernel");
    }

    [Fact]
    public void AddSemanticKernel_WithMissingProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Semantic Kernel provider is required");
        exception.Message.Should().Contain("Provider");
    }

    [Fact]
    public void AddSemanticKernel_WithUnknownProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "UnknownProvider",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Unknown LLM provider");
        exception.Message.Should().Contain("UnknownProvider");
        exception.Message.Should().Contain("Valid values");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProviderMissingConfiguration_ThrowsInvalidOperationException()
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
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("OpenAI configuration is missing");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProviderMissingApiKey_ThrowsInvalidOperationException()
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
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("OpenAI API key is required");
        exception.Message.Should().Contain("ApiKey");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProviderMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("OpenAI model ID is required");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiProviderInvalidModel_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "invalid-model-xyz"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("not in the list of supported models");
        exception.Message.Should().Contain("invalid-model-xyz");
    }

    [Fact]
    public void AddSemanticKernel_WithAzureOpenAiProviderMissingConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Azure OpenAI configuration is missing");
    }

    [Fact]
    public void AddSemanticKernel_WithAzureOpenAiProviderMissingEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Azure OpenAI endpoint is required");
    }

    [Fact]
    public void AddSemanticKernel_WithAzureOpenAiProviderMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Azure OpenAI API key is required");
    }

    [Fact]
    public void AddSemanticKernel_WithAzureOpenAiProviderMissingDeploymentName_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "test-key"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Azure OpenAI deployment name is required");
    }

    [Fact]
    public void AddSemanticKernel_WithOllamaProviderMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Ollama",
                ["GmsdAgents:SemanticKernel:Ollama:BaseUrl"] = "http://localhost:11434"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Ollama model ID is required");
    }

    [Fact]
    public void AddSemanticKernel_WithAnthropicProviderMissingConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Anthropic configuration is missing");
    }

    [Fact]
    public void AddSemanticKernel_WithAnthropicProviderMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic",
                ["GmsdAgents:SemanticKernel:Anthropic:ModelId"] = "claude-3-sonnet-20240229"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Anthropic API key is required");
    }

    [Fact]
    public void AddSemanticKernel_WithAnthropicProviderMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic",
                ["GmsdAgents:SemanticKernel:Anthropic:ApiKey"] = "test-key"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddSemanticKernel(configuration));

        exception.Message.Should().Contain("Anthropic model ID is required");
    }

    #endregion

    #region Exception on Missing Provider Configuration Tests

    [Fact]
    public void AddOpenAiChatCompletion_WithMissingConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddOpenAiChatCompletion(configuration));

        exception.Message.Should().Contain("OpenAI configuration is missing");
    }

    [Fact]
    public void AddOpenAiChatCompletion_WithMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddOpenAiChatCompletion(configuration));

        exception.Message.Should().Contain("OpenAI API key is required");
    }

    [Fact]
    public void AddOpenAiChatCompletion_WithMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAi:ApiKey"] = "test-key"
            })
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddOpenAiChatCompletion(configuration));

        exception.Message.Should().Contain("OpenAI model ID is required");
    }

    [Fact]
    public void AddAzureOpenAiChatCompletion_WithMissingConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAzureOpenAiChatCompletion(configuration));

        exception.Message.Should().Contain("Azure OpenAI configuration is missing");
    }

    [Fact]
    public void AddAzureOpenAiChatCompletion_WithMissingEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureOpenAi:ApiKey"] = "test-key",
                ["AzureOpenAi:DeploymentName"] = "gpt-4"
            })
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAzureOpenAiChatCompletion(configuration));

        exception.Message.Should().Contain("Azure OpenAI endpoint is required");
    }

    [Fact]
    public void AddAzureOpenAiChatCompletion_WithMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["AzureOpenAi:DeploymentName"] = "gpt-4"
            })
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAzureOpenAiChatCompletion(configuration));

        exception.Message.Should().Contain("Azure OpenAI API key is required");
    }

    [Fact]
    public void AddAzureOpenAiChatCompletion_WithMissingDeploymentName_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["AzureOpenAi:ApiKey"] = "test-key"
            })
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAzureOpenAiChatCompletion(configuration));

        exception.Message.Should().Contain("Azure OpenAI deployment name is required");
    }

    [Fact]
    public void AddOllamaChatCompletion_WithMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange - Configuration section exists but ModelId is empty
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = "http://localhost:11434"
            })
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddOllamaChatCompletion(configuration));

        exception.Message.Should().Contain("Ollama model ID is required");
    }

    [Fact]
    public void AddAnthropicChatCompletion_WithMissingConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAnthropicChatCompletion(configuration));

        exception.Message.Should().Contain("Anthropic configuration is missing");
    }

    [Fact]
    public void AddAnthropicChatCompletion_WithMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ModelId"] = "claude-3-sonnet-20240229"
            })
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAnthropicChatCompletion(configuration));

        exception.Message.Should().Contain("Anthropic API key is required");
    }

    [Fact]
    public void AddAnthropicChatCompletion_WithMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "test-key"
            })
            .Build();

        var builder = Kernel.CreateBuilder();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            builder.AddAnthropicChatCompletion(configuration));

        exception.Message.Should().Contain("Anthropic model ID is required");
    }

    #endregion

    #region Service Registration Tests

    [Fact]
    public void AddSemanticKernel_RegistersOptionsWithCorrectSection()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var options = serviceProvider.GetService<IOptions<SemanticKernelOptions>>();
        options.Should().NotBeNull();
        options!.Value.Provider.Should().Be("OpenAi");
    }

    [Fact]
    public void AddSemanticKernel_RegistersKernelAsSingleton()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var kernel1 = serviceProvider.GetService<Kernel>();
        var kernel2 = serviceProvider.GetService<Kernel>();

        kernel1.Should().NotBeNull();
        kernel2.Should().NotBeNull();
        kernel1.Should().BeSameAs(kernel2); // Singleton means same instance
    }

    [Fact]
    public void AddSemanticKernel_RegistersToolRegistryAsSingleton()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var registry = serviceProvider.GetService<IToolRegistry>();
        registry.Should().NotBeNull();
        registry.Should().BeOfType<ToolRegistry>();
    }

    [Fact]
    public void AddSemanticKernel_RegistersChatCompletionService()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var chatCompletionService = serviceProvider.GetService<IChatCompletionService>();
        chatCompletionService.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticKernel_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        var result = services.AddSemanticKernel(configuration);

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddOpenAiChatCompletion_ReturnsKernelBuilderForChaining()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenAi:ApiKey"] = "test-key",
                ["OpenAi:ModelId"] = "gpt-4o"
            })
            .Build();

        var services = new ServiceCollection();
        var builder = Kernel.CreateBuilder();

        // Act
        var result = builder.AddOpenAiChatCompletion(configuration);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddAzureOpenAiChatCompletion_ReturnsKernelBuilderForChaining()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["AzureOpenAi:ApiKey"] = "test-key",
                ["AzureOpenAi:DeploymentName"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();
        var builder = Kernel.CreateBuilder();

        // Act
        var result = builder.AddAzureOpenAiChatCompletion(configuration);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddOllamaChatCompletion_ReturnsKernelBuilderForChaining()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:ModelId"] = "llama3"
            })
            .Build();

        var services = new ServiceCollection();
        var builder = Kernel.CreateBuilder();

        // Act
        var result = builder.AddOllamaChatCompletion(configuration);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void AddAnthropicChatCompletion_ReturnsKernelBuilderForChaining()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Anthropic:ApiKey"] = "test-key",
                ["Anthropic:ModelId"] = "claude-3-sonnet-20240229"
            })
            .Build();

        var services = new ServiceCollection();
        var builder = Kernel.CreateBuilder();

        // Act
        var result = builder.AddAnthropicChatCompletion(configuration);

        // Assert
        result.Should().BeSameAs(builder);
    }

    [Fact]
    public void RegisterTool_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.RegisterTool<FakeTool>();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void ScanAndRegisterTools_ReturnsServiceCollectionForChaining()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var result = services.ScanAndRegisterTools();

        // Assert
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void ScanAndRegisterTools_RegistersToolsFromAssembly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Scan the current test assembly which has ScannableFakeTool
        services.ScanAndRegisterTools(typeof(SemanticKernelServiceCollectionExtensionsTests).Assembly);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var tools = serviceProvider.GetServices<ITool>().ToList();
        tools.Should().Contain(t => t.GetType() == typeof(Gmsd.Agents.Tests.Fakes.ScannableFakeTool));
    }

    [Fact]
    public void RegisterTool_RegistersSpecificToolType()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        services.RegisterTool<FakeTool>();
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var tool = serviceProvider.GetService<ITool>();
        tool.Should().NotBeNull();
        tool.Should().BeOfType<FakeTool>();
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Fake tool implementation for testing tool registration.
    /// </summary>
    public class FakeTool : ITool
    {
        public Task<ToolResult> InvokeAsync(ToolRequest request, ToolContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ToolResult.Success("Executed"));
        }
    }

    #endregion
}
