using Gmsd.Aos.Contracts.Secrets;
using Gmsd.Aos.Engine.Secrets;
using Xunit;

namespace Gmsd.Aos.Tests.Engine.Secrets;

public class MockSecretStoreTests
{
    [Fact]
    public async Task SetSecretAsync_StoresSecret()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("test-key", "test-value");
        var value = await store.GetSecretAsync("test-key");
        Assert.Equal("test-value", value);
    }

    [Fact]
    public async Task GetSecretAsync_ThrowsWhenSecretNotFound()
    {
        var store = new MockSecretStore();
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => store.GetSecretAsync("nonexistent"));
    }

    [Fact]
    public async Task DeleteSecretAsync_RemovesSecret()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("test-key", "test-value");
        await store.DeleteSecretAsync("test-key");
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => store.GetSecretAsync("test-key"));
    }

    [Fact]
    public async Task DeleteSecretAsync_ThrowsWhenSecretNotFound()
    {
        var store = new MockSecretStore();
        await Assert.ThrowsAsync<SecretNotFoundException>(
            () => store.DeleteSecretAsync("nonexistent"));
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsTrueWhenSecretExists()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("test-key", "test-value");
        var exists = await store.SecretExistsAsync("test-key");
        Assert.True(exists);
    }

    [Fact]
    public async Task SecretExistsAsync_ReturnsFalseWhenSecretNotExists()
    {
        var store = new MockSecretStore();
        var exists = await store.SecretExistsAsync("nonexistent");
        Assert.False(exists);
    }

    [Fact]
    public async Task ListSecretsAsync_ReturnsAllSecretNames()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("key1", "value1");
        await store.SetSecretAsync("key2", "value2");
        await store.SetSecretAsync("key3", "value3");

        var secrets = await store.ListSecretsAsync();
        Assert.Equal(3, secrets.Count);
        Assert.Contains("key1", secrets);
        Assert.Contains("key2", secrets);
        Assert.Contains("key3", secrets);
    }

    [Fact]
    public async Task ListSecretsAsync_ReturnsEmptyWhenNoSecrets()
    {
        var store = new MockSecretStore();
        var secrets = await store.ListSecretsAsync();
        Assert.Empty(secrets);
    }

    [Fact]
    public async Task SetSecretAsync_ThrowsWhenNameIsEmpty()
    {
        var store = new MockSecretStore();
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SetSecretAsync("", "value"));
    }

    [Fact]
    public async Task SetSecretAsync_ThrowsWhenValueIsEmpty()
    {
        var store = new MockSecretStore();
        await Assert.ThrowsAsync<ArgumentException>(
            () => store.SetSecretAsync("name", ""));
    }

    [Fact]
    public async Task Clear_RemovesAllSecrets()
    {
        var store = new MockSecretStore();
        await store.SetSecretAsync("key1", "value1");
        await store.SetSecretAsync("key2", "value2");

        store.Clear();
        Assert.Equal(0, store.GetSecretCount());
    }

    [Fact]
    public async Task GetSecretCount_ReturnsCorrectCount()
    {
        var store = new MockSecretStore();
        Assert.Equal(0, store.GetSecretCount());

        await store.SetSecretAsync("key1", "value1");
        Assert.Equal(1, store.GetSecretCount());

        await store.SetSecretAsync("key2", "value2");
        Assert.Equal(2, store.GetSecretCount());

        await store.DeleteSecretAsync("key1");
        Assert.Equal(1, store.GetSecretCount());
    }
}
