using Gmsd.Agents.Configuration;
using Gmsd.Aos.Configuration;
using Gmsd.Aos.Composition;
using Gmsd.Aos.Engine.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Gmsd.Agents.Tests.Configuration;

public class SemanticKernelSecretInjectionTests
{
    [Fact]
    public async Task SemanticKernelConfiguration_ResolvesSecretReferencesInApiKey()
    {
        var services = new ServiceCollection();
        var secretStore = new MockSecretStore();
        
        // Set up a secret in the store
        await secretStore.SetSecretAsync("openai-api-key", "sk-test-openai-key");

        // Configure with secret reference
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "GmsdAgents:SemanticKernel:Provider", "OpenAi" },
                { "GmsdAgents:SemanticKernel:OpenAi:ApiKey", "$secret:openai-api-key" },
                { "GmsdAgents:SemanticKernel:OpenAi:ModelId", "gpt-4" }
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton(secretStore);
        services.AddSecretManagement();

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{ ""apiKey"": ""$secret:openai-api-key"" }";
        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-test-openai-key", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task SemanticKernelConfiguration_ResolvesMultipleProviderSecrets()
    {
        var secretStore = new MockSecretStore();
        
        await secretStore.SetSecretAsync("openai-key", "sk-openai-value");
        await secretStore.SetSecretAsync("anthropic-key", "sk-anthropic-value");
        await secretStore.SetSecretAsync("azure-key", "sk-azure-value");

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{
            ""openAi"": { ""apiKey"": ""$secret:openai-key"" },
            ""anthropic"": { ""apiKey"": ""$secret:anthropic-key"" },
            ""azureOpenAi"": { ""apiKey"": ""$secret:azure-key"" }
        }";

        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-openai-value", resolved);
        Assert.Contains("sk-anthropic-value", resolved);
        Assert.Contains("sk-azure-value", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task SemanticKernelConfiguration_ThrowsWhenSecretNotFound()
    {
        var secretStore = new MockSecretStore();
        var resolver = new SecretConfigurationResolver(secretStore);

        var configJson = @"{ ""apiKey"": ""$secret:missing-key"" }";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(configJson));

        Assert.Contains("missing-key", ex.Message);
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public async Task SemanticKernelConfiguration_PreservesNonSecretConfiguration()
    {
        var secretStore = new MockSecretStore();
        await secretStore.SetSecretAsync("api-key", "sk-secret");

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{
            ""apiKey"": ""$secret:api-key"",
            ""modelId"": ""gpt-4"",
            ""temperature"": 0.7,
            ""maxTokens"": 2048
        }";

        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-secret", resolved);
        Assert.Contains("gpt-4", resolved);
        Assert.Contains("0.7", resolved);
        Assert.Contains("2048", resolved);
    }

    [Fact]
    public async Task SecretAwareConfigurationProvider_LoadAndResolveAsync()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "config.json");
            var configContent = @"{
                ""apiKey"": ""$secret:test-key"",
                ""modelId"": ""gpt-4""
            }";
            await File.WriteAllTextAsync(configPath, configContent);

            var secretStore = new MockSecretStore();
            await secretStore.SetSecretAsync("test-key", "sk-resolved-value");

            var options = new SecretConfigurationOptions { EnableSecretResolution = true };
            var provider = new SecretAwareConfigurationProvider(secretStore, options);

            var resolved = await provider.LoadAndResolveAsync(configPath);

            Assert.Contains("sk-resolved-value", resolved);
            Assert.DoesNotContain("$secret:", resolved);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SecretAwareConfigurationProvider_SkipsResolutionWhenDisabled()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var configPath = Path.Combine(tempDir, "config.json");
            var configContent = @"{ ""apiKey"": ""$secret:test-key"" }";
            await File.WriteAllTextAsync(configPath, configContent);

            var secretStore = new MockSecretStore();
            var options = new SecretConfigurationOptions { EnableSecretResolution = false };
            var provider = new SecretAwareConfigurationProvider(secretStore, options);

            var resolved = await provider.LoadAndResolveAsync(configPath);

            // Should still contain the secret reference since resolution is disabled
            Assert.Contains("$secret:test-key", resolved);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void SecretAwareConfigurationProvider_GetSafeForLogging_MasksSecrets()
    {
        var config = @"{
            ""apiKey"": ""$secret:openai-key"",
            ""endpoint"": ""https://api.openai.com""
        }";

        var safe = SecretAwareConfigurationProvider.GetSafeForLogging(config);

        Assert.Contains("$secret:openai-key", safe);
        Assert.Contains("https://api.openai.com", safe);
    }

    [Fact]
    public void SecretAwareConfigurationProvider_ContainsSecretReferences()
    {
        var configWithSecrets = @"{ ""apiKey"": ""$secret:key"" }";
        var configWithoutSecrets = @"{ ""apiKey"": ""sk-plaintext"" }";

        Assert.True(SecretAwareConfigurationProvider.ContainsSecretReferences(configWithSecrets));
        Assert.False(SecretAwareConfigurationProvider.ContainsSecretReferences(configWithoutSecrets));
    }

    [Fact]
    public async Task SemanticKernelConfiguration_IntegrationWithDependencyInjection()
    {
        var services = new ServiceCollection();
        var secretStore = new MockSecretStore();
        
        await secretStore.SetSecretAsync("openai-api-key", "sk-test-key");

        services.AddSingleton(secretStore);
        services.AddSecretManagement();

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<SecretConfigurationResolver>();

        var configJson = @"{ ""apiKey"": ""$secret:openai-api-key"" }";
        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-test-key", resolved);
    }

    [Fact]
    public async Task SemanticKernelConfiguration_MigratesAndResolvesSecrets()
    {
        var secretStore = new MockSecretStore();
        var migrationUtility = new SecretMigrationUtility(secretStore);

        // Start with plaintext configuration
        var plaintextConfig = @"{
            ""openAi"": {
                ""apiKey"": ""sk-plaintext-openai"",
                ""modelId"": ""gpt-4""
            }
        }";

        // Migrate to secret references
        var migratedConfig = await migrationUtility.MigrateConfigurationAsync(plaintextConfig);

        // Verify the plaintext key is no longer in the config
        Assert.DoesNotContain("sk-plaintext-openai", migratedConfig);
        Assert.Contains("$secret:", migratedConfig);

        // Now resolve the secret references
        var resolver = new SecretConfigurationResolver(secretStore);
        var resolvedConfig = await resolver.ResolveAsync(migratedConfig);

        // Verify the secret is resolved
        Assert.Contains("sk-plaintext-openai", resolvedConfig);
        Assert.DoesNotContain("$secret:", resolvedConfig);
    }

    [Fact]
    public async Task SemanticKernelConfiguration_HandlesComplexLlmProviderConfig()
    {
        var secretStore = new MockSecretStore();
        
        await secretStore.SetSecretAsync("openai-key", "sk-openai");
        await secretStore.SetSecretAsync("azure-key", "sk-azure");
        await secretStore.SetSecretAsync("anthropic-key", "sk-anthropic");

        var resolver = new SecretConfigurationResolver(secretStore);
        var complexConfig = @"{
            ""GmsdAgents"": {
                ""SemanticKernel"": {
                    ""Provider"": ""OpenAi"",
                    ""OpenAi"": {
                        ""ApiKey"": ""$secret:openai-key"",
                        ""ModelId"": ""gpt-4"",
                        ""Temperature"": 0.7
                    },
                    ""AzureOpenAi"": {
                        ""ApiKey"": ""$secret:azure-key"",
                        ""Endpoint"": ""https://example.openai.azure.com"",
                        ""DeploymentName"": ""gpt-4-deployment""
                    },
                    ""Anthropic"": {
                        ""ApiKey"": ""$secret:anthropic-key"",
                        ""ModelId"": ""claude-3-sonnet-20240229""
                    }
                }
            }
        }";

        var resolved = await resolver.ResolveAsync(complexConfig);

        Assert.Contains("sk-openai", resolved);
        Assert.Contains("sk-azure", resolved);
        Assert.Contains("sk-anthropic", resolved);
        Assert.Contains("gpt-4", resolved);
        Assert.Contains("claude-3-sonnet-20240229", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }
}
