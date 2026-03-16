using System.Text.Json;
using FluentAssertions;
using nirmata.Agents.Execution.ControlPlane;
using nirmata.Agents.Execution.Validation;
using nirmata.Agents.Tests.Fakes;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Public;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Validation;

public sealed class PrerequisiteValidatorTests
{
    [Fact]
    public async Task EnsureWorkspaceInitializedAsync_WhenStateStoreSucceeds_ReturnsSatisfiedResult()
    {
        using var workspace = new FakeWorkspace();
        var stateStore = new FakeStateStore(workspace.RepositoryRootPath);
        var sut = new PrerequisiteValidator(workspace, stateStore);

        var result = await sut.EnsureWorkspaceInitializedAsync();

        result.IsSatisfied.Should().BeTrue();
        result.TargetPhase.Should().Be("WorkspaceInitialization");
        result.Missing.Should().BeNull();
    }

    [Fact]
    public async Task EnsureWorkspaceInitializedAsync_WhenStateStoreThrows_ReturnsStructuredDiagnostics()
    {
        using var workspace = new FakeWorkspace();
        var sut = new PrerequisiteValidator(workspace, new ThrowingInitializationStateStore());

        var result = await sut.EnsureWorkspaceInitializedAsync();

        result.IsSatisfied.Should().BeFalse();
        result.TargetPhase.Should().Be("WorkspaceInitialization");
        result.Missing.Should().NotBeNull();

        var missing = result.Missing!;
        missing.Type.Should().Be(PrerequisiteType.State);
        missing.FailureCode.Should().Be("state-readiness-failure");
        missing.FailingPrerequisite.Should().Be(".aos/state/state.json");
        missing.AttemptedRepairs.Should().ContainInOrder(
            "Ensure .aos/state/events.ndjson exists",
            "Ensure .aos/state/state.json exists with deterministic baseline",
            "Derive deterministic state snapshot from ordered events when snapshot is missing or stale");
        missing.SuggestedFixes.Should().NotBeEmpty();
        missing.SuggestedCommand.Should().Be("/init");
        missing.RecoveryAction.Should().Be("Repair workspace state artifacts and retry the command");
        missing.ConversationalPrompt.Should().NotBeNullOrWhiteSpace();
    }

    private sealed class ThrowingInitializationStateStore : IStateStore
    {
        public void EnsureWorkspaceInitialized()
            => throw new InvalidOperationException("boom");

        public StateSnapshot ReadSnapshot()
            => new() { SchemaVersion = 1, Cursor = new StateCursor() };

        public void AppendEvent(JsonElement payload)
        {
        }

        public StateEventTailResponse TailEvents(StateEventTailRequest request)
            => new() { Items = Array.Empty<StateEventEntry>() };
    }
}
