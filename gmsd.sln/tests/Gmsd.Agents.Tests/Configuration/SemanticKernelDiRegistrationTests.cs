using FluentAssertions;
using Gmsd.Agents.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Xunit;

namespace Gmsd.Agents.Tests.Configuration;

public class SemanticKernelDiRegistrationTests
{
    #region Valid Registration Tests

    [Fact]
    public void AddSemanticKernel_WithValidOpenAiConfiguration_RegistersServicesSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test-key",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var kernel = serviceProvider.GetService<Kernel>();
        kernel.Should().NotBeNull();

        var chatCompletionService = serviceProvider.GetService<IChatCompletionService>();
        chatCompletionService.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticKernel_WithValidAzureOpenAiConfiguration_RegistersServicesSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var kernel = serviceProvider.GetService<Kernel>();
        kernel.Should().NotBeNull();

        var chatCompletionService = serviceProvider.GetService<IChatCompletionService>();
        chatCompletionService.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticKernel_WithValidOllamaConfiguration_RegistersServicesSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Ollama",
                ["GmsdAgents:SemanticKernel:Ollama:ModelId"] = "llama3"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var kernel = serviceProvider.GetService<Kernel>();
        kernel.Should().NotBeNull();

        var chatCompletionService = serviceProvider.GetService<IChatCompletionService>();
        chatCompletionService.Should().NotBeNull();
    }

    [Fact]
    public void AddSemanticKernel_WithValidAnthropicConfiguration_RegistersServicesSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic",
                ["GmsdAgents:SemanticKernel:Anthropic:ApiKey"] = "sk-ant-test",
                ["GmsdAgents:SemanticKernel:Anthropic:ModelId"] = "claude-3-opus-20240229"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var kernel = serviceProvider.GetService<Kernel>();
        kernel.Should().NotBeNull();

        var chatCompletionService = serviceProvider.GetService<IChatCompletionService>();
        chatCompletionService.Should().NotBeNull();
    }

    #endregion

    #region Missing Configuration Tests

    [Fact]
    public void AddSemanticKernel_WithMissingConfiguration_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("Semantic Kernel configuration is missing");
        ex.Message.Should().Contain(SemanticKernelOptions.SectionName);
    }

    [Fact]
    public void AddSemanticKernel_WithEmptyProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = ""
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("provider is required");
    }

    [Fact]
    public void AddSemanticKernel_WithInvalidProvider_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "InvalidProvider"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("Unknown LLM provider");
        ex.Message.Should().Contain("InvalidProvider");
    }

    #endregion

    #region OpenAI Missing Fields Tests

    [Fact]
    public void AddSemanticKernel_WithOpenAiMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("OpenAI API key");
        ex.Message.Should().Contain("required");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("OpenAI model ID");
        ex.Message.Should().Contain("required");
    }

    [Fact]
    public void AddSemanticKernel_WithOpenAiUnsupportedModel_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "unsupported-model"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("not in the list of supported models");
        ex.Message.Should().Contain("unsupported-model");
    }

    [Theory]
    [InlineData("gpt-4")]
    [InlineData("gpt-4-turbo")]
    [InlineData("gpt-4-turbo-preview")]
    [InlineData("gpt-4o")]
    [InlineData("gpt-3.5-turbo")]
    public void AddSemanticKernel_WithOpenAiSupportedModel_RegistersSuccessfully(string modelId)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = modelId
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var kernel = serviceProvider.GetService<Kernel>();
        kernel.Should().NotBeNull();
    }

    #endregion

    #region Azure OpenAI Missing Fields Tests

    [Fact]
    public void AddSemanticKernel_WithAzureOpenAiMissingEndpoint_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("Azure OpenAI endpoint");
        ex.Message.Should().Contain("required");
    }

    [Fact]
    public void AddSemanticKernel_WithAzureOpenAiMissingApiKey_ThrowsInvalidOperationException()
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
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("Azure OpenAI API key");
        ex.Message.Should().Contain("required");
    }

    [Fact]
    public void AddSemanticKernel_WithAzureOpenAiMissingDeploymentName_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "azure-key"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("Azure OpenAI deployment name");
        ex.Message.Should().Contain("required");
    }

    #endregion

    #region Ollama Missing Fields Tests

    [Fact]
    public void AddSemanticKernel_WithOllamaMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Ollama"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("Ollama model ID");
        ex.Message.Should().Contain("required");
    }

    #endregion

    #region Anthropic Missing Fields Tests

    [Fact]
    public void AddSemanticKernel_WithAnthropicMissingApiKey_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic",
                ["GmsdAgents:SemanticKernel:Anthropic:ModelId"] = "claude-3-opus-20240229"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("Anthropic API key");
        ex.Message.Should().Contain("required");
    }

    [Fact]
    public void AddSemanticKernel_WithAnthropicMissingModelId_ThrowsInvalidOperationException()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic",
                ["GmsdAgents:SemanticKernel:Anthropic:ApiKey"] = "sk-ant-test"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSemanticKernel(configuration);
        });

        ex.Message.Should().Contain("Anthropic model ID");
        ex.Message.Should().Contain("required");
    }

    #endregion

    #region Service Lifetime Tests

    [Fact]
    public void AddSemanticKernel_RegistersKernelAsSingleton()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        var kernel1 = serviceProvider.GetRequiredService<Kernel>();
        var kernel2 = serviceProvider.GetRequiredService<Kernel>();

        // Assert
        kernel1.Should().BeSameAs(kernel2);
    }

    [Fact]
    public void AddSemanticKernel_RegistersChatCompletionServiceAsScoped()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        using var scope1 = serviceProvider.CreateScope();
        using var scope2 = serviceProvider.CreateScope();

        var service1 = scope1.ServiceProvider.GetRequiredService<IChatCompletionService>();
        var service2 = scope2.ServiceProvider.GetRequiredService<IChatCompletionService>();

        // Assert - scoped services should be different across scopes
        service1.Should().NotBeSameAs(service2);
    }

    #endregion

    #region Case Insensitivity Tests

    [Theory]
    [InlineData("openai")]
    [InlineData("OpenAi")]
    [InlineData("OPENAI")]
    public void AddSemanticKernel_WithCaseInsensitiveProvider_RegistersSuccessfully(string providerName)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = providerName,
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddSemanticKernel(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var kernel = serviceProvider.GetService<Kernel>();
        kernel.Should().NotBeNull();
    }

    #endregion
}
