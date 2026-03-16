using nirmata.Aos.Engine.Secrets;
using Xunit;

namespace nirmata.Aos.Tests.Engine.Secrets;

public class SecretMigrationUtilityTests
{
    [Fact]
    public async Task MigrateConfigurationAsync_MigratesPlaintextApiKey()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{ ""apiKey"": ""sk-test-plaintext-key"" }";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.DoesNotContain("sk-test-plaintext-key", migrated);
        Assert.Contains("$secret:", migrated);

        var storedSecret = await store.GetSecretAsync("api-key");
        Assert.Equal("sk-test-plaintext-key", storedSecret);
    }

    [Fact]
    public async Task MigrateConfigurationAsync_MigratesMultipleApiKeys()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{
            ""openAiApiKey"": ""sk-openai-plaintext"",
            ""anthropicApiKey"": ""sk-anthropic-plaintext""
        }";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.DoesNotContain("sk-openai-plaintext", migrated);
        Assert.DoesNotContain("sk-anthropic-plaintext", migrated);
        Assert.Contains("$secret:open-ai-api-key", migrated);
        Assert.Contains("$secret:anthropic-api-key", migrated);

        var openAiSecret = await store.GetSecretAsync("open-ai-api-key");
        Assert.Equal("sk-openai-plaintext", openAiSecret);

        var anthropicSecret = await store.GetSecretAsync("anthropic-api-key");
        Assert.Equal("sk-anthropic-plaintext", anthropicSecret);
    }

    [Fact]
    public async Task MigrateConfigurationAsync_SkipsAlreadyMigratedSecrets()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{ ""apiKey"": ""$secret:already-migrated"" }";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.Contains("$secret:already-migrated", migrated);
        Assert.Equal(0, store.GetSecretCount());
    }

    [Fact]
    public async Task MigrateConfigurationAsync_HandlesNestedObjects()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{
            ""providers"": {
                ""openAi"": {
                    ""apiKey"": ""sk-nested-key""
                }
            }
        }";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.DoesNotContain("sk-nested-key", migrated);
        Assert.Contains("$secret:", migrated);

        var storedSecret = await store.GetSecretAsync("api-key");
        Assert.Equal("sk-nested-key", storedSecret);
    }

    [Fact]
    public async Task MigrateConfigurationAsync_PreservesNonKeyValues()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{
            ""apiKey"": ""sk-secret-key"",
            ""endpoint"": ""https://api.example.com"",
            ""modelId"": ""gpt-4""
        }";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.Contains("https://api.example.com", migrated);
        Assert.Contains("gpt-4", migrated);
        Assert.DoesNotContain("sk-secret-key", migrated);
    }

    [Fact]
    public async Task MigrateConfigurationAsync_LogsMigrations()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{
            ""apiKey"": ""sk-key1"",
            ""token"": ""token-value""
        }";
        await utility.MigrateConfigurationAsync(config);

        var log = utility.GetMigrationLog();
        Assert.NotEmpty(log);
        Assert.Contains(log, entry => entry.Contains("apiKey"));
        Assert.Contains(log, entry => entry.Contains("token"));
    }

    [Fact]
    public async Task MigrateConfigurationAsync_ClearsMigrationLog()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{ ""apiKey"": ""sk-key"" }";
        await utility.MigrateConfigurationAsync(config);

        var log = utility.GetMigrationLog();
        Assert.NotEmpty(log);

        utility.ClearMigrationLog();
        var clearedLog = utility.GetMigrationLog();
        Assert.Empty(clearedLog);
    }

    [Fact]
    public async Task MigrateConfigurationAsync_HandlesEmptyConfiguration()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{}";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.NotNull(migrated);
        Assert.Equal(0, store.GetSecretCount());
    }

    [Fact]
    public async Task MigrateConfigurationAsync_HandlesNullConfiguration()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var migrated = await utility.MigrateConfigurationAsync(null!);

        Assert.Null(migrated);
    }

    [Fact]
    public async Task MigrateConfigurationAsync_ThrowsOnInvalidJson()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var invalidConfig = @"{ invalid json }";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => utility.MigrateConfigurationAsync(invalidConfig));
    }

    [Fact]
    public async Task MigrateConfigurationAsync_MigratesPasswordField()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{ ""password"": ""secret-password-123"" }";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.DoesNotContain("secret-password-123", migrated);
        Assert.Contains("$secret:", migrated);

        var storedSecret = await store.GetSecretAsync("password");
        Assert.Equal("secret-password-123", storedSecret);
    }

    [Fact]
    public async Task MigrateConfigurationAsync_MigratesTokenField()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{ ""token"": ""bearer-token-xyz"" }";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.DoesNotContain("bearer-token-xyz", migrated);
        Assert.Contains("$secret:", migrated);

        var storedSecret = await store.GetSecretAsync("token");
        Assert.Equal("bearer-token-xyz", storedSecret);
    }

    [Fact]
    public async Task MigrateConfigurationAsync_HandlesArraysWithoutMigration()
    {
        var store = new MockSecretStore();
        var utility = new SecretMigrationUtility(store);

        var config = @"{ ""items"": [""item1"", ""item2""] }";
        var migrated = await utility.MigrateConfigurationAsync(config);

        Assert.Contains("item1", migrated);
        Assert.Contains("item2", migrated);
        Assert.Equal(0, store.GetSecretCount());
    }
}
