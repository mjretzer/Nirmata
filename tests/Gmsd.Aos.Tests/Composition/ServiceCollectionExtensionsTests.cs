using Gmsd.Aos.Composition;
using Gmsd.Aos.Configuration;
using Gmsd.Aos.Engine.Stores;
using Gmsd.Aos.Engine.Validation;
using Gmsd.Aos.Public;
using Gmsd.Aos.Public.Catalogs;
using Gmsd.Aos.Public.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Gmsd.Aos.Tests.Composition;

public class ServiceCollectionExtensionsTests
{
    private static IServiceCollection CreateTestServices(string repositoryRootPath)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GmsdAos:RepositoryRootPath"] = repositoryRootPath
            })
            .Build();

        services.AddGmsdAos(configuration);
        return services;
    }

    private static string CreateTempRepositoryRoot()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(Path.Combine(tempPath, ".aos"));
        return tempPath;
    }

    [Fact]
    public void AddGmsdAos_RegistersAosOptions()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<IOptions<AosOptions>>();

        Assert.NotNull(options);
        Assert.Equal(repositoryRoot, options.Value.RepositoryRootPath);
    }

    [Fact]
    public void AddGmsdAos_RegistersIWorkspace_AsSingleton()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();
        var workspace1 = provider.GetService<IWorkspace>();
        var workspace2 = provider.GetService<IWorkspace>();

        Assert.NotNull(workspace1);
        Assert.Same(workspace1, workspace2);
    }

    [Fact]
    public void AddGmsdAos_RegistersISpecStore_AsSingleton()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();
        var store1 = provider.GetService<ISpecStore>();
        var store2 = provider.GetService<ISpecStore>();

        Assert.NotNull(store1);
        Assert.Same(store1, store2);
    }

    [Fact]
    public void AddGmsdAos_RegistersIStateStore_AsSingleton()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();
        var store1 = provider.GetService<IStateStore>();
        var store2 = provider.GetService<IStateStore>();

        Assert.NotNull(store1);
        Assert.Same(store1, store2);
    }

    [Fact]
    public void AddGmsdAos_RegistersIEvidenceStore_AsSingleton()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();
        var store1 = provider.GetService<IEvidenceStore>();
        var store2 = provider.GetService<IEvidenceStore>();

        Assert.NotNull(store1);
        Assert.Same(store1, store2);
    }

    [Fact]
    public void AddGmsdAos_RegistersIValidator_AsSingleton()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();
        var validator1 = provider.GetService<IValidator>();
        var validator2 = provider.GetService<IValidator>();

        Assert.NotNull(validator1);
        Assert.Same(validator1, validator2);
    }

    [Fact]
    public void AddGmsdAos_RegistersCommandCatalog_AsSingleton()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();
        var catalog1 = provider.GetService<CommandCatalog>();
        var catalog2 = provider.GetService<CommandCatalog>();

        Assert.NotNull(catalog1);
        Assert.Same(catalog1, catalog2);
    }

    [Fact]
    public void AddGmsdAos_RegistersICommandRouter_AsScoped()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();

        // Same scope - should return same instance
        using (var scope1 = provider.CreateScope())
        {
            var router1a = scope1.ServiceProvider.GetService<ICommandRouter>();
            var router1b = scope1.ServiceProvider.GetService<ICommandRouter>();

            Assert.NotNull(router1a);
            Assert.Same(router1a, router1b);
        }

        // Different scopes - should return different instances
        using (var scope2 = provider.CreateScope())
        using (var scope3 = provider.CreateScope())
        {
            var router2 = scope2.ServiceProvider.GetService<ICommandRouter>();
            var router3 = scope3.ServiceProvider.GetService<ICommandRouter>();

            Assert.NotNull(router2);
            Assert.NotNull(router3);
            Assert.NotSame(router2, router3);
        }
    }

    [Fact]
    public void AddGmsdAos_WithPathOverload_RegistersAllServices()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = new ServiceCollection();

        services.AddGmsdAos(repositoryRoot);

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IWorkspace>());
        Assert.NotNull(provider.GetService<ISpecStore>());
        Assert.NotNull(provider.GetService<IStateStore>());
        Assert.NotNull(provider.GetService<IEvidenceStore>());
        Assert.NotNull(provider.GetService<IValidator>());
        Assert.NotNull(provider.GetService<CommandCatalog>());
        Assert.NotNull(provider.GetService<ICommandRouter>());
    }

    [Fact]
    public void AddGmsdAos_StoresShareWorkspacePath()
    {
        var repositoryRoot = CreateTempRepositoryRoot();
        var services = CreateTestServices(repositoryRoot);

        var provider = services.BuildServiceProvider();
        var workspace = provider.GetRequiredService<IWorkspace>();

        // Verify all stores can be resolved and workspace is properly configured
        Assert.NotNull(provider.GetRequiredService<ISpecStore>());
        Assert.NotNull(provider.GetRequiredService<IStateStore>());
        Assert.NotNull(provider.GetRequiredService<IEvidenceStore>());
        Assert.Equal(repositoryRoot, workspace.RepositoryRootPath);
    }
}
