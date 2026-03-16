using Xunit;

namespace nirmata.Agents.Tests.Contracts;

using nirmata.Agents.Tests.Fakes;
using nirmata.Aos.Public;

/// <summary>
/// Contract tests that validate all AOS fake implementations correctly implement their interfaces.
/// These tests use reflection to ensure method signatures, return types, and properties match.
/// </summary>
public class AosFakeContractTests
{
    [Fact]
    public void ValidateFake_FakeEventStore_ImplementsIEventStore()
    {
        FakeValidator.ValidateFake<FakeEventStore, IEventStore>();
    }

    [Fact]
    public void ValidateFake_FakeStateStore_ImplementsIStateStore()
    {
        FakeValidator.ValidateFake<FakeStateStore, IStateStore>();
    }

    [Fact]
    public void ValidateFake_FakeWorkspace_ImplementsIWorkspace()
    {
        FakeValidator.ValidateFake<FakeWorkspace, IWorkspace>();
    }
}
