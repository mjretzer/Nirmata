using nirmata.Agents.Configuration;
using nirmata.Aos.Configuration;
using nirmata.Aos.Composition;
using nirmata.Aos.Engine.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace nirmata.Agents.Tests.Execution.ControlPlane.Llm;

public class LlmProviderSecretInjectionTests
{
    [Fact]
    public async Task LlmProviderConfiguration_InjectsSecretIntoOpenAiProvider()
    {
        var secretStore = new MockSecretStore();
        await secretStore.SetSecretAsync("openai-api-key", "sk-test-openai-key-12345");

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{
            ""Provider"": ""OpenAi"",
            ""OpenAi"": {
                ""ApiKey"": ""$secret:openai-api-key"",
                ""ModelId"": ""gpt-4""
            }
        }";

        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-test-openai-key-12345", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task LlmProviderConfiguration_InjectsSecretIntoAzureOpenAiProvider()
    {
        var secretStore = new MockSecretStore();
        await secretStore.SetSecretAsync("azure-openai-key", "sk-azure-test-key");

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{
            ""Provider"": ""AzureOpenAi"",
            ""AzureOpenAi"": {
                ""ApiKey"": ""$secret:azure-openai-key"",
                ""Endpoint"": ""https://example.openai.azure.com"",
                ""DeploymentName"": ""gpt-4-deployment""
            }
        }";

        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-azure-test-key", resolved);
        Assert.Contains("https://example.openai.azure.com", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task LlmProviderConfiguration_InjectsSecretIntoAnthropicProvider()
    {
        var secretStore = new MockSecretStore();
        await secretStore.SetSecretAsync("anthropic-api-key", "sk-ant-test-key");

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{
            ""Provider"": ""Anthropic"",
            ""Anthropic"": {
                ""ApiKey"": ""$secret:anthropic-api-key"",
                ""ModelId"": ""claude-3-sonnet-20240229""
            }
        }";

        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-ant-test-key", resolved);
        Assert.Contains("claude-3-sonnet-20240229", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task LlmProviderConfiguration_HandlesMultipleSecretsInSingleProvider()
    {
        var secretStore = new MockSecretStore();
        await secretStore.SetSecretAsync("primary-key", "sk-primary");
        await secretStore.SetSecretAsync("backup-key", "sk-backup");

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{
            ""Provider"": ""OpenAi"",
            ""OpenAi"": {
                ""ApiKey"": ""$secret:primary-key"",
                ""BackupKey"": ""$secret:backup-key"",
                ""ModelId"": ""gpt-4""
            }
        }";

        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-primary", resolved);
        Assert.Contains("sk-backup", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task LlmProviderConfiguration_FailsGracefullyWhenSecretMissing()
    {
        var secretStore = new MockSecretStore();
        var resolver = new SecretConfigurationResolver(secretStore);

        var configJson = @"{
            ""Provider"": ""OpenAi"",
            ""OpenAi"": {
                ""ApiKey"": ""$secret:missing-key"",
                ""ModelId"": ""gpt-4""
            }
        }";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(configJson));

        Assert.Contains("missing-key", ex.Message);
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public async Task LlmProviderConfiguration_MigrateAndInjectSecrets()
    {
        var secretStore = new MockSecretStore();
        var migrationUtility = new SecretMigrationUtility(secretStore);

        // Start with plaintext configuration
        var plaintextConfig = @"{
            ""Provider"": ""OpenAi"",
            ""OpenAi"": {
                ""ApiKey"": ""sk-plaintext-key-12345"",
                ""ModelId"": ""gpt-4""
            }
        }";

        // Migrate to secret references
        var migratedConfig = await migrationUtility.MigrateConfigurationAsync(plaintextConfig);
        Assert.DoesNotContain("sk-plaintext-key-12345", migratedConfig);
        Assert.Contains("$secret:", migratedConfig);

        // Resolve the secret references
        var resolver = new SecretConfigurationResolver(secretStore);
        var resolvedConfig = await resolver.ResolveAsync(migratedConfig);

        // Verify the secret is injected
        Assert.Contains("sk-plaintext-key-12345", resolvedConfig);
        Assert.DoesNotContain("$secret:", resolvedConfig);
    }

    [Fact]
    public async Task LlmProviderConfiguration_PreservesNonSecretProviderSettings()
    {
        var secretStore = new MockSecretStore();
        await secretStore.SetSecretAsync("api-key", "sk-secret");

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{
            ""Provider"": ""OpenAi"",
            ""OpenAi"": {
                ""ApiKey"": ""$secret:api-key"",
                ""ModelId"": ""gpt-4"",
                ""Temperature"": 0.7,
                ""MaxTokens"": 2048,
                ""TopP"": 0.9,
                ""FrequencyPenalty"": 0.0,
                ""PresencePenalty"": 0.0
            }
        }";

        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-secret", resolved);
        Assert.Contains("gpt-4", resolved);
        Assert.Contains("0.7", resolved);
        Assert.Contains("2048", resolved);
        Assert.Contains("0.9", resolved);
    }

    [Fact]
    public async Task LlmProviderConfiguration_IntegrationWithServiceCollection()
    {
        var services = new ServiceCollection();
        var secretStore = new MockSecretStore();

        await secretStore.SetSecretAsync("openai-key", "sk-integration-test");

        services.AddSingleton(secretStore);
        services.AddSecretManagement();

        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<SecretConfigurationResolver>();

        var configJson = @"{ ""apiKey"": ""$secret:openai-key"" }";
        var resolved = await resolver.ResolveAsync(configJson);

        Assert.Contains("sk-integration-test", resolved);
    }

    [Fact]
    public async Task LlmProviderConfiguration_SecureLoggingWithMaskedSecrets()
    {
        var config = @"{
            ""Provider"": ""OpenAi"",
            ""OpenAi"": {
                ""ApiKey"": ""$secret:openai-key"",
                ""ModelId"": ""gpt-4""
            }
        }";

        var safeForLogging = SecretAwareConfigurationProvider.GetSafeForLogging(config);

        // Should contain the secret reference (masked) but not the actual value
        Assert.Contains("$secret:openai-key", safeForLogging);
        Assert.Contains("gpt-4", safeForLogging);
    }

    [Fact]
    public async Task LlmProviderConfiguration_RotatesSecretsWithoutRestart()
    {
        var secretStore = new MockSecretStore();
        await secretStore.SetSecretAsync("api-key", "sk-old-key");

        var resolver = new SecretConfigurationResolver(secretStore);
        var configJson = @"{ ""apiKey"": ""$secret:api-key"" }";

        var resolved1 = await resolver.ResolveAsync(configJson);
        Assert.Contains("sk-old-key", resolved1);

        // Rotate the secret
        await secretStore.SetSecretAsync("api-key", "sk-new-key");

        var resolved2 = await resolver.ResolveAsync(configJson);
        Assert.Contains("sk-new-key", resolved2);
        Assert.DoesNotContain("sk-old-key", resolved2);
    }

    [Fact]
    public async Task LlmProviderConfiguration_HandlesCompleteMultiProviderSetup()
    {
        var secretStore = new MockSecretStore();
        await secretStore.SetSecretAsync("openai-key", "sk-openai-prod");
        await secretStore.SetSecretAsync("azure-key", "sk-azure-prod");
        await secretStore.SetSecretAsync("anthropic-key", "sk-anthropic-prod");

        var resolver = new SecretConfigurationResolver(secretStore);
        var fullConfig = @"{
            ""nirmataAgents"": {
                ""SemanticKernel"": {
                    ""Provider"": ""OpenAi"",
                    ""OpenAi"": {
                        ""ApiKey"": ""$secret:openai-key"",
                        ""ModelId"": ""gpt-4"",
                        ""Temperature"": 0.7,
                        ""MaxTokens"": 4096
                    },
                    ""AzureOpenAi"": {
                        ""ApiKey"": ""$secret:azure-key"",
                        ""Endpoint"": ""https://prod.openai.azure.com"",
                        ""DeploymentName"": ""gpt-4-prod""
                    },
                    ""Anthropic"": {
                        ""ApiKey"": ""$secret:anthropic-key"",
                        ""ModelId"": ""claude-3-opus-20240229""
                    }
                }
            }
        }";

        var resolved = await resolver.ResolveAsync(fullConfig);

        Assert.Contains("sk-openai-prod", resolved);
        Assert.Contains("sk-azure-prod", resolved);
        Assert.Contains("sk-anthropic-prod", resolved);
        Assert.Contains("gpt-4", resolved);
        Assert.Contains("claude-3-opus-20240229", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task LlmProviderConfiguration_MigrationPreservesAllSettings()
    {
        var secretStore = new MockSecretStore();
        var migrationUtility = new SecretMigrationUtility(secretStore);

        var plaintextConfig = @"{
            ""Provider"": ""OpenAi"",
            ""OpenAi"": {
                ""ApiKey"": ""sk-plaintext-key"",
                ""ModelId"": ""gpt-4"",
                ""Temperature"": 0.7,
                ""MaxTokens"": 2048,
                ""TopP"": 0.9
            }
        }";

        var migratedConfig = await migrationUtility.MigrateConfigurationAsync(plaintextConfig);

        // Verify non-secret settings are preserved
        Assert.Contains("gpt-4", migratedConfig);
        Assert.Contains("0.7", migratedConfig);
        Assert.Contains("2048", migratedConfig);
        Assert.Contains("0.9", migratedConfig);

        // Verify plaintext key is migrated
        Assert.DoesNotContain("sk-plaintext-key", migratedConfig);
        Assert.Contains("$secret:", migratedConfig);
    }
}
