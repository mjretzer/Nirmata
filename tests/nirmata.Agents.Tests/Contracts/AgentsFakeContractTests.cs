using Xunit;

namespace nirmata.Agents.Tests.Contracts;

using nirmata.Agents.Execution.Brownfield.CodebaseScanner;
using nirmata.Agents.Execution.Brownfield.SymbolCacheBuilder;
using nirmata.Agents.Execution.ControlPlane.Llm.Contracts;
using nirmata.Agents.Execution.Execution.AtomicGitCommitter;
using nirmata.Agents.Persistence.Runs;
using nirmata.Agents.Tests.Fakes;

/// <summary>
/// Contract tests that validate all agent-level fake implementations correctly implement their interfaces.
/// These tests use reflection to ensure method signatures, return types, and properties match.
/// </summary>
public class AgentsFakeContractTests
{
    [Fact]
    public void ValidateFake_FakeAtomicGitCommitter_ImplementsIAtomicGitCommitter()
    {
        FakeValidator.ValidateFake<FakeAtomicGitCommitter, IAtomicGitCommitter>();
    }

    [Fact]
    public void ValidateFake_FakeCodebaseScanner_ImplementsICodebaseScanner()
    {
        FakeValidator.ValidateFake<FakeCodebaseScanner, ICodebaseScanner>();
    }

    [Fact]
    public void ValidateFake_FakeLlmProvider_ImplementsILlmProvider()
    {
        FakeValidator.ValidateFake<FakeLlmProvider, ILlmProvider>();
    }

    [Fact]
    public void ValidateFake_FakeRunLifecycleManager_ImplementsIRunLifecycleManager()
    {
        FakeValidator.ValidateFake<FakeRunLifecycleManager, IRunLifecycleManager>();
    }

    [Fact]
    public void ValidateFake_FakeSymbolCacheBuilder_ImplementsISymbolCacheBuilder()
    {
        FakeValidator.ValidateFake<FakeSymbolCacheBuilder, ISymbolCacheBuilder>();
    }
}
