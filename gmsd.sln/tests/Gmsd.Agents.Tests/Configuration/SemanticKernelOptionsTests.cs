using FluentAssertions;
using Gmsd.Agents.Configuration;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Agents.Tests.Configuration;

public class SemanticKernelOptionsTests
{
    #region Valid Configuration Tests

    [Fact]
    public void ValidOpenAiConfiguration_LoadsSuccessfully()
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

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Provider.Should().Be("OpenAi");
        options.OpenAi.Should().NotBeNull();
        options.OpenAi!.ApiKey.Should().Be("sk-test-key");
        options.OpenAi.ModelId.Should().Be("gpt-4");
    }

    [Fact]
    public void ValidAzureOpenAiConfiguration_LoadsSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4-deployment"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Provider.Should().Be("AzureOpenAi");
        options.AzureOpenAi.Should().NotBeNull();
        options.AzureOpenAi!.Endpoint.Should().Be("https://test.openai.azure.com");
        options.AzureOpenAi.ApiKey.Should().Be("azure-key");
        options.AzureOpenAi.DeploymentName.Should().Be("gpt-4-deployment");
    }

    [Fact]
    public void ValidOllamaConfiguration_LoadsSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Ollama",
                ["GmsdAgents:SemanticKernel:Ollama:ModelId"] = "llama3"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Provider.Should().Be("Ollama");
        options.Ollama.Should().NotBeNull();
        options.Ollama!.ModelId.Should().Be("llama3");
    }

    [Fact]
    public void ValidAnthropicConfiguration_LoadsSuccessfully()
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

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Provider.Should().Be("Anthropic");
        options.Anthropic.Should().NotBeNull();
        options.Anthropic!.ApiKey.Should().Be("sk-ant-test");
        options.Anthropic.ModelId.Should().Be("claude-3-opus-20240229");
    }

    #endregion

    #region Missing Required Fields Tests

    [Fact]
    public void MissingProvider_ReturnsNull()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().BeNull();
    }

    [Fact]
    public void EmptyProvider_ReturnsOptionsWithEmptyProvider()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = ""
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Provider.Should().Be("");
    }

    [Fact]
    public void MissingOpenAiApiKey_ReturnsOptionsWithoutApiKey()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.OpenAi.Should().NotBeNull();
        options.OpenAi!.ApiKey.Should().Be("");
    }

    [Fact]
    public void MissingOpenAiModelId_ReturnsOptionsWithoutModelId()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.OpenAi.Should().NotBeNull();
        options.OpenAi!.ModelId.Should().Be("");
    }

    #endregion

    #region OpenAI Configuration Tests

    [Fact]
    public void OpenAiConfiguration_WithAllOptionalFields_LoadsSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4",
                ["GmsdAgents:SemanticKernel:OpenAi:OrganizationId"] = "org-123",
                ["GmsdAgents:SemanticKernel:OpenAi:BaseUrl"] = "https://custom.openai.com",
                ["GmsdAgents:SemanticKernel:OpenAi:Temperature"] = "0.7",
                ["GmsdAgents:SemanticKernel:OpenAi:MaxTokens"] = "4096",
                ["GmsdAgents:SemanticKernel:OpenAi:TopP"] = "0.9",
                ["GmsdAgents:SemanticKernel:OpenAi:FrequencyPenalty"] = "0.5",
                ["GmsdAgents:SemanticKernel:OpenAi:PresencePenalty"] = "0.3",
                ["GmsdAgents:SemanticKernel:OpenAi:Seed"] = "42",
                ["GmsdAgents:SemanticKernel:OpenAi:EnableParallelToolCalls"] = "false"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.OpenAi.Should().NotBeNull();
        options.OpenAi!.OrganizationId.Should().Be("org-123");
        options.OpenAi.BaseUrl.Should().Be("https://custom.openai.com");
        options.OpenAi.Temperature.Should().Be(0.7);
        options.OpenAi.MaxTokens.Should().Be(4096);
        options.OpenAi.TopP.Should().Be(0.9);
        options.OpenAi.FrequencyPenalty.Should().Be(0.5);
        options.OpenAi.PresencePenalty.Should().Be(0.3);
        options.OpenAi.Seed.Should().Be(42);
        options.OpenAi.EnableParallelToolCalls.Should().BeFalse();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void OpenAiConfiguration_WithValidTemperature_LoadsSuccessfully(double temperature)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4",
                ["GmsdAgents:SemanticKernel:OpenAi:Temperature"] = temperature.ToString()
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.OpenAi!.Temperature.Should().Be(temperature);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void OpenAiConfiguration_WithValidTopP_LoadsSuccessfully(double topP)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "OpenAi",
                ["GmsdAgents:SemanticKernel:OpenAi:ApiKey"] = "sk-test",
                ["GmsdAgents:SemanticKernel:OpenAi:ModelId"] = "gpt-4",
                ["GmsdAgents:SemanticKernel:OpenAi:TopP"] = topP.ToString()
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.OpenAi!.TopP.Should().Be(topP);
    }

    [Fact]
    public void OpenAiConfiguration_WithDefaultValues_LoadsSuccessfully()
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

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.OpenAi.Should().NotBeNull();
        options.OpenAi!.Temperature.Should().Be(1.0);
        options.OpenAi.MaxTokens.Should().Be(2048);
        options.OpenAi.TopP.Should().Be(1.0);
        options.OpenAi.FrequencyPenalty.Should().Be(0.0);
        options.OpenAi.PresencePenalty.Should().Be(0.0);
        options.OpenAi.EnableParallelToolCalls.Should().BeTrue();
        options.OpenAi.Seed.Should().BeNull();
    }

    #endregion

    #region Azure OpenAI Configuration Tests

    [Fact]
    public void AzureOpenAiConfiguration_WithDefaultApiVersion_LoadsSuccessfully()
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

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.AzureOpenAi.Should().NotBeNull();
        options.AzureOpenAi!.ApiVersion.Should().Be("2024-02-01");
    }

    [Fact]
    public void AzureOpenAiConfiguration_WithCustomApiVersion_LoadsSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "AzureOpenAi",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:Endpoint"] = "https://test.openai.azure.com",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiKey"] = "azure-key",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:DeploymentName"] = "gpt-4",
                ["GmsdAgents:SemanticKernel:AzureOpenAi:ApiVersion"] = "2024-06-01"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.AzureOpenAi!.ApiVersion.Should().Be("2024-06-01");
    }

    #endregion

    #region Ollama Configuration Tests

    [Fact]
    public void OllamaConfiguration_WithDefaultBaseUrl_LoadsSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Ollama",
                ["GmsdAgents:SemanticKernel:Ollama:ModelId"] = "llama3"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Ollama.Should().NotBeNull();
        options.Ollama!.BaseUrl.Should().Be("http://localhost:11434");
    }

    [Fact]
    public void OllamaConfiguration_WithCustomBaseUrl_LoadsSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Ollama",
                ["GmsdAgents:SemanticKernel:Ollama:BaseUrl"] = "http://192.168.1.100:11434",
                ["GmsdAgents:SemanticKernel:Ollama:ModelId"] = "mistral"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Ollama!.BaseUrl.Should().Be("http://192.168.1.100:11434");
        options.Ollama.ModelId.Should().Be("mistral");
    }

    #endregion

    #region Anthropic Configuration Tests

    [Fact]
    public void AnthropicConfiguration_WithDefaultApiVersion_LoadsSuccessfully()
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

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Anthropic.Should().NotBeNull();
        options.Anthropic!.ApiVersion.Should().Be("2023-06-01");
    }

    [Fact]
    public void AnthropicConfiguration_WithCustomBaseUrl_LoadsSuccessfully()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAgents:SemanticKernel:Provider"] = "Anthropic",
                ["GmsdAgents:SemanticKernel:Anthropic:ApiKey"] = "sk-ant-test",
                ["GmsdAgents:SemanticKernel:Anthropic:ModelId"] = "claude-3-sonnet-20240229",
                ["GmsdAgents:SemanticKernel:Anthropic:BaseUrl"] = "https://custom.anthropic.com"
            })
            .Build();

        // Act
        var options = configuration
            .GetSection(SemanticKernelOptions.SectionName)
            .Get<SemanticKernelOptions>();

        // Assert
        options.Should().NotBeNull();
        options!.Anthropic!.BaseUrl.Should().Be("https://custom.anthropic.com");
    }

    #endregion

    #region Section Name Tests

    [Fact]
    public void SectionName_IsCorrect()
    {
        // Assert
        SemanticKernelOptions.SectionName.Should().Be("GmsdAgents:SemanticKernel");
    }

    #endregion
}
