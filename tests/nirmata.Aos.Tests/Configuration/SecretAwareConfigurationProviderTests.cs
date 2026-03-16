using nirmata.Aos.Configuration;
using nirmata.Aos.Engine.Secrets;
using Xunit;

namespace nirmata.Aos.Tests.Configuration;

public class SecretAwareConfigurationProviderTests
{
    [Fact]
    public async Task ResolveAsync_ResolvesSecretReferences()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("api-key", "sk-secret-value");

        var options = new SecretConfigurationOptions { EnableSecretResolution = true };
        var provider = new SecretAwareConfigurationProvider(store, options);

        var config = @"{ ""apiKey"": ""$secret:api-key"" }";
        var resolved = await provider.ResolveAsync(config);

        Assert.Contains("sk-secret-value", resolved);
    }

    [Fact]
    public async Task ResolveAsync_SkipsResolutionWhenDisabled()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("api-key", "sk-secret-value");

        var options = new SecretConfigurationOptions { EnableSecretResolution = false };
        var provider = new SecretAwareConfigurationProvider(store, options);

        var config = @"{ ""apiKey"": ""$secret:api-key"" }";
        var resolved = await provider.ResolveAsync(config);

        Assert.Contains("$secret:api-key", resolved);
        Assert.DoesNotContain("sk-secret-value", resolved);
    }

    [Fact]
    public async Task LoadAndResolveAsync_LoadsAndResolvesFromFile()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var store = new MockSecretStore();
            await store.SetSecretAsync("db-password", "secret-db-pass");

            var config = @"{ ""database"": { ""password"": ""$secret:db-password"" } }";
            await File.WriteAllTextAsync(tempFile, config);

            var options = new SecretConfigurationOptions { EnableSecretResolution = true };
            var provider = new SecretAwareConfigurationProvider(store, options);

            var resolved = await provider.LoadAndResolveAsync(tempFile);
            Assert.Contains("secret-db-pass", resolved);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAndResolveAsync_ThrowsWhenFileNotFound()
    {
        var store = new MockSecretStore();
        var options = new SecretConfigurationOptions();
        var provider = new SecretAwareConfigurationProvider(store, options);

        await Assert.ThrowsAsync<FileNotFoundException>(
            () => provider.LoadAndResolveAsync("/nonexistent/path/config.json"));
    }

    [Fact]
    public void GetSafeForLogging_MasksSecrets()
    {
        var config = @"{ ""apiKey"": ""$secret:openai-key"", ""endpoint"": ""https://api.example.com"" }";
        var safe = SecretAwareConfigurationProvider.GetSafeForLogging(config);

        Assert.Contains("$secret:openai-key", safe);
        Assert.Contains("https://api.example.com", safe);
    }

    [Fact]
    public void ContainsSecretReferences_ReturnsTrueWhenReferencesExist()
    {
        var config = @"{ ""apiKey"": ""$secret:openai-key"" }";
        Assert.True(SecretAwareConfigurationProvider.ContainsSecretReferences(config));
    }

    [Fact]
    public void ContainsSecretReferences_ReturnsFalseWhenNoReferences()
    {
        var config = @"{ ""apiKey"": ""sk-test-value"" }";
        Assert.False(SecretAwareConfigurationProvider.ContainsSecretReferences(config));
    }
}
