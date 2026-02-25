using Gmsd.Aos.Engine.StateTransitions;
using Gmsd.Aos.Engine.Workspace;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosStateTransitionEngineTests
{
    [Fact]
    public void ValidateTransitionOrThrow_WhenKindUnknown_DoesNotMutateStateArtifacts()
    {
        var tempRoot = CreateTempDirectory();
        try
        {
            AosWorkspaceBootstrapper.EnsureInitialized(tempRoot);

            var aosRootPath = Path.Combine(tempRoot, ".aos");
            var statePath = Path.Combine(aosRootPath, "state", "state.json");
            var eventsPath = Path.Combine(aosRootPath, "state", "events.ndjson");

            var stateBefore = File.ReadAllBytes(statePath);
            var eventsBefore = File.ReadAllBytes(eventsPath);

            Assert.Throws<AosInvalidStateTransitionException>(() =>
                AosStateTransitionEngine.ValidateTransitionOrThrow(aosRootPath, "transition.not.allowed")
            );

            Assert.Equal(stateBefore, File.ReadAllBytes(statePath));
            Assert.Equal(eventsBefore, File.ReadAllBytes(eventsPath));
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    private static string CreateTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "gmsd-aos-transition-engine", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }
}

