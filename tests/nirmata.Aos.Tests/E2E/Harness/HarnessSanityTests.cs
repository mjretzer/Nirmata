namespace nirmata.Aos.Tests.E2E.Harness;

using System.IO;
using System.Threading.Tasks;
using nirmata.TestTargets;
using Xunit;

/// <summary>
/// Sanity tests proving the E2E harness works correctly.
/// These tests verify that FixtureRepo, AosTestHarness, and all utilities function properly.
/// </summary>
public class HarnessSanityTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task Harness_CreatesFixture_AndRunsAosInit()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // Act
        var result = await harness.RunAsync("init");

        // Assert
        Assert.True(result.ExitCode == 0, $"Expected exit code 0 but got {result.ExitCode}. StdOut: {result.StdOut}, StdErr: {result.StdErr}");
        Assert.True(Directory.Exists(Path.Combine(fixture.RootPath, ".aos")), ".aos/ directory should exist after init");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Harness_AssertLayout_ValidatesAllLayers()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // Act
        var result = await harness.RunAsync("init");

        // Assert
        Assert.Equal(0, result.ExitCode);
        harness.AssertLayout(); // Should not throw - all 6 layers exist
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Harness_ReadState_CanDeserializeProjectJson()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // Act
        var result = await harness.RunAsync("init");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var projectSpec = harness.ReadState<object>("spec/project.json");
        Assert.NotNull(projectSpec);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Harness_ReadEventsTail_ReturnsEvents()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // Act
        var result = await harness.RunAsync("init");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var events = harness.ReadEventsTail(10);
        Assert.NotNull(events);
        // Events may or may not exist depending on implementation, but should not throw
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task FixtureRepo_CreatesAndCleansUpTempDirectory()
    {
        // Arrange
        string? rootPath = null;

        // Act
        using (var fixture = FixtureRepo.Create())
        {
            rootPath = fixture.RootPath;
            Assert.True(Directory.Exists(rootPath), "Fixture directory should exist during test");

            // Create a test file to verify write access
            var testFile = Path.Combine(rootPath, "test.txt");
            File.WriteAllText(testFile, "test");
            Assert.True(File.Exists(testFile), "Should be able to write files to fixture");
        }

        // Assert
        Assert.NotNull(rootPath);
        Assert.False(Directory.Exists(rootPath), "Fixture directory should be cleaned up after disposal");
    }
}
