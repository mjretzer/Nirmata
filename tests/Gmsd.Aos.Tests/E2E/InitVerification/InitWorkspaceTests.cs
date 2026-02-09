namespace Gmsd.Aos.Tests.E2E.InitVerification;

using System.IO;
using System.Threading.Tasks;
using Gmsd.Aos.Tests.E2E.Harness;
using Gmsd.TestTargets;
using Xunit;

/// <summary>
/// E2E tests verifying that `aos init` creates a valid workspace with all required layers.
/// </summary>
public class InitWorkspaceTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task Init_CreatesAllSixLayers()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // Act
        var result = await harness.RunAsync("init");

        // Assert
        Assert.True(result.ExitCode == 0, $"Expected exit code 0 but got {result.ExitCode}. StdOut: {result.StdOut}, StdErr: {result.StdErr}");
        harness.AssertLayout(); // All 6 layers exist
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Init_CreatesProjectJson()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // Act
        var result = await harness.RunAsync("init");

        // Assert
        Assert.Equal(0, result.ExitCode);
        AssertAosLayout.AssertFileExists(fixture.RootPath, "spec/project.json");
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Init_CreatesValidProjectSpec()
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
    public async Task Init_SeedsMinimalCodebasePack()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // Act
        var result = await harness.RunAsync("init");

        // Assert
        Assert.Equal(0, result.ExitCode);
        AssertAosLayout.AssertLayerExists(fixture.RootPath, "codebase");
    }
}
