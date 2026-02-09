using FluentAssertions;
using Gmsd.Agents.Configuration;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gmsd.Agents.Tests;

public class LlmProviderDiTests
{
    [Theory]
    [InlineData("openai", typeof(Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.OpenAi.OpenAiLlmAdapter))]
    [InlineData("anthropic", typeof(Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.Anthropic.AnthropicLlmAdapter))]
    [InlineData("azure-openai", typeof(Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.AzureOpenAi.AzureOpenAiLlmAdapter))]
    [InlineData("ollama", typeof(Gmsd.Agents.Execution.ControlPlane.Llm.Adapters.Ollama.OllamaLlmAdapter))]
    public void AddLlmProvider_RegistersCorrectProviderByConfig(string providerName, Type expectedType)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:Llm:Provider"] = providerName,
                [$"Agents:Llm:{providerName}:ApiKey"] = "test-key",
                [$"Agents:Llm:{providerName}:Endpoint"] = "https://test.example.com",
                [$"Agents:Llm:{providerName}:BaseUrl"] = "http://localhost:11434"
            })
            .Build();

        var services = new ServiceCollection();

        // Act
        services.AddLlmProvider(configuration);
        var provider = services.BuildServiceProvider();
        var llmProvider = provider.GetService<ILlmProvider>();

        // Assert
        llmProvider.Should().NotBeNull();
        llmProvider.Should().BeOfType(expectedType);
    }

    [Fact]
    public void AddLlmProvider_ThrowsWhenProviderNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        // Act & Assert
        Action act = () => services.AddLlmProvider(configuration);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Agents:Llm:Provider*");
    }

    [Fact]
    public void AddLlmProvider_ThrowsWhenProviderIsUnknown()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agents:Llm:Provider"] = "unknown-provider"
            })
            .Build();

        var services = new ServiceCollection();

        // Act & Assert
        Action act = () => services.AddLlmProvider(configuration);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Unknown LLM provider*");
    }
}
