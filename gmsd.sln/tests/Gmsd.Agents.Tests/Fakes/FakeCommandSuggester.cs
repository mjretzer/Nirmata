using Gmsd.Agents.Execution.Preflight.CommandSuggestion;

namespace Gmsd.Agents.Tests.Fakes;

public class FakeCommandSuggester : ICommandSuggester
{
    public Task<CommandProposal?> SuggestAsync(string input, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<CommandProposal?>(null);
    }
}
