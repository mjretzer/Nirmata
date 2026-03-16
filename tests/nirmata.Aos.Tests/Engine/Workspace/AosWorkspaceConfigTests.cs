using System;
using System.Collections.Generic;
using System.IO;
using nirmata.Aos.Engine.Workspace;
using Xunit;

namespace nirmata.Aos.Tests.Engine.Workspace;

public class AosWorkspaceConfigTests : IDisposable
{
    private readonly string _testWorkspaceRoot;

    public AosWorkspaceConfigTests()
    {
        _testWorkspaceRoot = Path.Combine(Path.GetTempPath(), $"aos-config-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testWorkspaceRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testWorkspaceRoot))
        {
            Directory.Delete(_testWorkspaceRoot, recursive: true);
        }
    }

    [Fact]
    public void ReadWorkspaceConfig_WithValidConfig_ReturnsConfig()
    {
        // Arrange
        AosWorkspaceBootstrapper.EnsureInitialized(_testWorkspaceRoot);
        var config = new AosWorkspaceConfigDocument(
            SchemaVersion: 1,
            AgentPreferences: new Dictionary<string, object> { { "key1", "value1" } },
            EngineOverrides: new Dictionary<string, object> { { "override1", "val" } },
            ExcludedPaths: new[] { "/path1", "/path2" }
        );

        AosWorkspaceBootstrapper.WriteWorkspaceConfig(_testWorkspaceRoot, config);

        // Act
        var readConfig = AosWorkspaceBootstrapper.ReadWorkspaceConfig(_testWorkspaceRoot);

        // Assert
        Assert.NotNull(readConfig);
        Assert.Equal(1, readConfig.SchemaVersion);
        Assert.Contains("key1", readConfig.AgentPreferences.Keys);
        Assert.Contains("override1", readConfig.EngineOverrides.Keys);
        Assert.Equal(2, readConfig.ExcludedPaths.Count);
    }

    [Fact]
    public void WriteWorkspaceConfig_CreatesConfigFile()
    {
        // Arrange
        AosWorkspaceBootstrapper.EnsureInitialized(_testWorkspaceRoot);
        var config = new AosWorkspaceConfigDocument(
            SchemaVersion: 1,
            AgentPreferences: new Dictionary<string, object>(),
            EngineOverrides: new Dictionary<string, object>(),
            ExcludedPaths: []
        );

        // Act
        var success = AosWorkspaceBootstrapper.WriteWorkspaceConfig(_testWorkspaceRoot, config);

        // Assert
        Assert.True(success);
        var configPath = Path.Combine(_testWorkspaceRoot, ".aos", "config", "workspace.json");
        Assert.True(File.Exists(configPath));
    }

    [Fact]
    public void ReadWorkspaceConfig_WithMissingFile_ReturnsNull()
    {
        // Arrange
        AosWorkspaceBootstrapper.EnsureInitialized(_testWorkspaceRoot);

        // Act
        var config = AosWorkspaceBootstrapper.ReadWorkspaceConfig(_testWorkspaceRoot);

        // Assert
        Assert.Null(config);
    }
}
