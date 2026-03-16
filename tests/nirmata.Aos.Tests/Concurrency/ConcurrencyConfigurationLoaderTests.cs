using System.Text.Json;
using nirmata.Aos.Configuration;
using nirmata.Aos.Public;
using Microsoft.Extensions.Logging;
using Xunit;

namespace nirmata.Aos.Tests.Concurrency;

public class ConcurrencyConfigurationLoaderTests
{
    private readonly string _testWorkspaceRoot;
    private readonly ILogger<ConcurrencyConfigurationLoader> _logger;

    public ConcurrencyConfigurationLoaderTests()
    {
        _testWorkspaceRoot = Path.Combine(Path.GetTempPath(), $"aos-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testWorkspaceRoot);
        _logger = new LoggerFactory().CreateLogger<ConcurrencyConfigurationLoader>();
    }

    [Fact]
    public void Load_WhenConfigFileDoesNotExist_ReturnsDefaults()
    {
        var workspace = new MockWorkspace(_testWorkspaceRoot);
        var loader = new ConcurrencyConfigurationLoader(workspace, _logger);

        var options = loader.Load();

        Assert.Equal(3, options.MaxParallelTasks);
        Assert.Equal(2, options.MaxParallelLlmCalls);
        Assert.Equal(10, options.TaskQueueSize);
    }

    [Fact]
    public void Load_WithValidConfigFile_ReturnsConfiguredValues()
    {
        var aosRoot = Path.Combine(_testWorkspaceRoot, ".aos");
        var configDir = Path.Combine(aosRoot, "config");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "concurrency.json");
        var configJson = @"{
  ""maxParallelTasks"": 5,
  ""maxParallelLlmCalls"": 3,
  ""taskQueueSize"": 15
}";
        File.WriteAllText(configPath, configJson);

        var workspace = new MockWorkspace(_testWorkspaceRoot);
        var loader = new ConcurrencyConfigurationLoader(workspace, _logger);

        var options = loader.Load();

        Assert.Equal(5, options.MaxParallelTasks);
        Assert.Equal(3, options.MaxParallelLlmCalls);
        Assert.Equal(15, options.TaskQueueSize);
    }

    [Fact]
    public void Load_WithInvalidConfig_ThrowsInvalidOperationException()
    {
        var aosRoot = Path.Combine(_testWorkspaceRoot, ".aos");
        var configDir = Path.Combine(aosRoot, "config");
        Directory.CreateDirectory(configDir);

        var configPath = Path.Combine(configDir, "concurrency.json");
        var configJson = @"{
  ""maxParallelTasks"": 0,
  ""maxParallelLlmCalls"": 2,
  ""taskQueueSize"": 10
}";
        File.WriteAllText(configPath, configJson);

        var workspace = new MockWorkspace(_testWorkspaceRoot);
        var loader = new ConcurrencyConfigurationLoader(workspace, _logger);

        Assert.Throws<InvalidOperationException>(() => loader.Load());
    }

    private class MockWorkspace : IWorkspace
    {
        private readonly string _testRoot;

        public MockWorkspace(string testRoot)
        {
            _testRoot = testRoot;
        }

        public string RepositoryRootPath => _testRoot;
        public string AosRootPath => Path.Combine(_testRoot, ".aos");

        public string GetContractPathForArtifactId(string artifactId)
        {
            throw new NotImplementedException();
        }

        public string GetAbsolutePathForContractPath(string contractPath)
        {
            throw new NotImplementedException();
        }

        public string GetAbsolutePathForArtifactId(string artifactId)
        {
            throw new NotImplementedException();
        }

        public JsonElement ReadArtifact(string subpath, string filename)
        {
            throw new NotImplementedException();
        }
    }
}
