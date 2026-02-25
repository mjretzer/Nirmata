using Gmsd.Aos.Contracts.Secrets;
using Gmsd.Aos.Engine.Secrets;
using Xunit;

namespace Gmsd.Aos.Tests.Engine.Secrets;

public class SecretConfigurationResolverTests
{
    [Fact]
    public async Task ResolveAsync_ResolvesSimpleSecretReference()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("openai-key", "sk-test-value");

        var resolver = new SecretConfigurationResolver(store);
        var config = @"{ ""apiKey"": ""$secret:openai-key"" }";
        var resolved = await resolver.ResolveAsync(config);

        Assert.Contains("sk-test-value", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task ResolveAsync_ResolvesMultipleSecretReferences()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("openai-key", "sk-openai");
        await store.SetSecretAsync("anthropic-key", "sk-anthropic");

        var resolver = new SecretConfigurationResolver(store);
        var config = @"{ ""openai"": ""$secret:openai-key"", ""anthropic"": ""$secret:anthropic-key"" }";
        var resolved = await resolver.ResolveAsync(config);

        Assert.Contains("sk-openai", resolved);
        Assert.Contains("sk-anthropic", resolved);
        Assert.DoesNotContain("$secret:", resolved);
    }

    [Fact]
    public async Task ResolveAsync_ThrowsWhenSecretNotFound()
    {
        var store = new MockSecretStore();
        var resolver = new SecretConfigurationResolver(store);
        var config = @"{ ""apiKey"": ""$secret:missing-key"" }";

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => resolver.ResolveAsync(config));
    }

    [Fact]
    public async Task ResolveAsync_PreservesNonSecretValues()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("api-key", "sk-secret");

        var resolver = new SecretConfigurationResolver(store);
        var config = @"{ ""apiKey"": ""$secret:api-key"", ""endpoint"": ""https://api.example.com"" }";
        var resolved = await resolver.ResolveAsync(config);

        Assert.Contains("sk-secret", resolved);
        Assert.Contains("https://api.example.com", resolved);
    }

    [Fact]
    public async Task ResolveAsync_HandlesNestedObjects()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("db-password", "secret-password");

        var resolver = new SecretConfigurationResolver(store);
        var config = @"{ ""database"": { ""password"": ""$secret:db-password"" } }";
        var resolved = await resolver.ResolveAsync(config);

        Assert.Contains("secret-password", resolved);
    }

    [Fact]
    public async Task ResolveAsync_HandlesArrays()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("key1", "value1");
        await store.SetSecretAsync("key2", "value2");

        var resolver = new SecretConfigurationResolver(store);
        var config = @"{ ""keys"": [""$secret:key1"", ""$secret:key2""] }";
        var resolved = await resolver.ResolveAsync(config);

        Assert.Contains("value1", resolved);
        Assert.Contains("value2", resolved);
    }

    [Fact]
    public void ContainsSecretReferences_ReturnsTrueWhenReferencesExist()
    {
        var config = @"{ ""apiKey"": ""$secret:openai-key"" }";
        Assert.True(SecretConfigurationResolver.ContainsSecretReferences(config));
    }

    [Fact]
    public void ContainsSecretReferences_ReturnsFalseWhenNoReferences()
    {
        var config = @"{ ""apiKey"": ""sk-test-value"" }";
        Assert.False(SecretConfigurationResolver.ContainsSecretReferences(config));
    }

    [Fact]
    public void MaskSecrets_MasksSecretReferences()
    {
        var config = @"{ ""apiKey"": ""$secret:openai-key"" }";
        var masked = SecretConfigurationResolver.MaskSecrets(config);

        Assert.Contains("$secret:openai-key", masked);
    }

    [Fact]
    public void MaskSecrets_PreservesNonSecretValues()
    {
        var config = @"{ ""apiKey"": ""sk-test-value"", ""endpoint"": ""https://api.example.com"" }";
        var masked = SecretConfigurationResolver.MaskSecrets(config);

        Assert.Contains("https://api.example.com", masked);
    }

    [Fact]
    public async Task ResolveAsync_HandlesEmptyConfig()
    {
        var store = new MockSecretStore();
        var resolver = new SecretConfigurationResolver(store);
        var config = @"{}";
        var resolved = await resolver.ResolveAsync(config);

        Assert.NotNull(resolved);
    }

    [Fact]
    public async Task ResolveAsync_HandlesEmptyString()
    {
        var store = new MockSecretStore();
        var resolver = new SecretConfigurationResolver(store);

        var result = await resolver.ResolveAsync("");
        Assert.Equal("", result);
    }
}
