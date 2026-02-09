using Gmsd.Aos.Engine.StateTransitions;
using Xunit;

namespace Gmsd.Aos.Tests;

public sealed class AosStateTransitionTableTests
{
    [Fact]
    public void CheckpointRestored_IsMarkedAsRollback_AndRequiresCheckpoint()
    {
        var rule = AosStateTransitionTable.Rules[AosStateTransitionTable.Kinds.CheckpointRestored];

        Assert.True(rule.MutatesStateSnapshot);
        Assert.True(rule.AppendsEvent);
        Assert.True(rule.RequiresCheckpoint);
    }
}

