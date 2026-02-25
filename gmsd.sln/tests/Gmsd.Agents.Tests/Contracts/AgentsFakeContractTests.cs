using Xunit;

namespace Gmsd.Agents.Tests.Contracts;

using Gmsd.Agents.Execution.Brownfield.CodebaseScanner;
using Gmsd.Agents.Execution.Brownfield.SymbolCacheBuilder;
using Gmsd.Agents.Execution.ControlPlane.Llm.Contracts;
using Gmsd.Agents.Execution.Execution.AtomicGitCommitter;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Agents.Tests.Fakes;

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
