namespace Gmsd.Aos.Tests.E2E.InitVerification;

using System.IO;
using System.Threading.Tasks;
using Gmsd.Aos.Tests.E2E.Harness;
using Gmsd.TestTargets;
using Xunit;

/// <summary>
/// E2E tests verifying that `aos init` is idempotent and safe to run multiple times.
/// </summary>
public class InitIdempotencyTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task Init_IsIdempotent()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // Act - Run init twice
        var result1 = await harness.RunAsync("init");
        var result2 = await harness.RunAsync("init");

        // Assert
        Assert.Equal(0, result1.ExitCode);
        Assert.Equal(0, result2.ExitCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Init_PreservesExistingState()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // First init
        await harness.RunAsync("init");
        
        // Create a marker file to simulate state
        var statePath = Path.Combine(fixture.RootPath, ".aos", "state", "state.json");
        var originalContent = File.ReadAllText(statePath);

        // Act - Run init again
        var result = await harness.RunAsync("init");

        // Assert
        Assert.Equal(0, result.ExitCode);
        var newContent = File.ReadAllText(statePath);
        Assert.Equal(originalContent, newContent);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task Init_NoDestructiveRewrite()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);

        // First init
        await harness.RunAsync("init");
        
        // Add a custom file in spec
        var customFilePath = Path.Combine(fixture.RootPath, ".aos", "spec", "custom.md");
        File.WriteAllText(customFilePath, "# Custom content");

        // Act - Run init again
        var result = await harness.RunAsync("init");

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(customFilePath), "Custom file should not be deleted");
        Assert.Equal("# Custom content", File.ReadAllText(customFilePath));
    }
}
