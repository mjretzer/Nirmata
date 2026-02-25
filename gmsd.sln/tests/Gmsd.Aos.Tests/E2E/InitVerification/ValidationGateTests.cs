namespace Gmsd.Aos.Tests.E2E.InitVerification;

using System.Threading.Tasks;
using Gmsd.Aos.Tests.E2E.Harness;
using Gmsd.TestTargets;
using Xunit;

/// <summary>
/// E2E tests verifying validation gates pass after init.
/// </summary>
public class ValidationGateTests
{
    [Fact]
    [Trait("Category", "E2E")]
    public async Task ValidateSchemas_SucceedsAfterInit()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);
        await harness.RunAsync("init");

        // Act
        var result = await harness.RunAsync("validate", "schemas");

        // Assert
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    [Trait("Category", "E2E")]
    public async Task ValidateWorkspace_SucceedsAfterInit()
    {
        // Arrange
        using var fixture = FixtureRepo.Create();
        var harness = new AosTestHarness(fixture.RootPath);
        await harness.RunAsync("init");

        // Act
        var result = await harness.RunAsync("validate", "workspace");

        // Assert
        Assert.Equal(0, result.ExitCode);
    }
}
