using FluentAssertions;
using nirmata.Agents.Configuration;
using nirmata.Agents.Execution.ControlPlane.Llm.Adapters;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace nirmata.Agents.Tests;

public class LlmProviderDiTests
{
    #region SemanticKernel Provider Resolution

    [Theory]
    [InlineData("OpenAi")]
    [InlineData("AzureOpenAi")]
    [InlineData("Ollama")]
    [InlineData("Anthropic")]
    public void AddLlmProvider_WithSemanticKernelConfiguration_RegistersSemanticKernelLlmProvider(string provider)
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SemanticKernelOptions.SectionName}:Provider"] = provider,
                [$"{SemanticKernelOptions.SectionName}:{provider}:ApiKey"] = "test-key",
                [$"{SemanticKernelOptions.SectionName}:{provider}:ModelId"] = "test-model"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());

        // Act
        services.AddLlmProvider(configuration);
        var providerService = services.BuildServiceProvider();
        var llmProvider = providerService.GetService<ILlmProvider>();

        // Assert
        llmProvider.Should().NotBeNull();
        llmProvider.Should().BeOfType<SemanticKernelLlmProvider>();
    }

    [Fact]
    public void AddLlmProvider_RegistersSingletonLlmProvider()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SemanticKernelOptions.SectionName}:Provider"] = "OpenAi",
                [$"{SemanticKernelOptions.SectionName}:OpenAi:ApiKey"] = "test-key",
                [$"{SemanticKernelOptions.SectionName}:OpenAi:ModelId"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());

        // Act
        services.AddLlmProvider(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Verify it's registered as singleton (same instance)
        var instance1 = serviceProvider.GetService<ILlmProvider>();
        var instance2 = serviceProvider.GetService<ILlmProvider>();
        instance1.Should().BeSameAs(instance2);
    }

    [Fact]
    public void AddLlmProvider_ThrowsWhenSemanticKernelProviderNotConfigured()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());

        // Act & Assert
        Action act = () => services.AddLlmProvider(configuration);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SemanticKernel*");
    }

    #endregion

    #region Provider Resolution

    [Fact]
    public void AddLlmProvider_ProviderName_ReturnsSemanticKernel()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SemanticKernelOptions.SectionName}:Provider"] = "OpenAi",
                [$"{SemanticKernelOptions.SectionName}:OpenAi:ApiKey"] = "test-key",
                [$"{SemanticKernelOptions.SectionName}:OpenAi:ModelId"] = "gpt-4"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddDebug());

        // Act
        services.AddLlmProvider(configuration);
        var provider = services.BuildServiceProvider();
        var llmProvider = provider.GetService<ILlmProvider>();

        // Assert
        llmProvider.Should().NotBeNull();
        llmProvider!.ProviderName.Should().Be("semantic-kernel");
    }

    #endregion
}
