namespace Gmsd.Agents.Execution.Preflight.CommandSuggestion;

/// <summary>
/// Service that analyzes natural language input and suggests explicit commands.
/// </summary>
public interface ICommandSuggester
{
    /// <summary>
    /// Analyzes the input and suggests a command if appropriate.
    /// </summary>
    /// <param name="input">The user input to analyze.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A command proposal if one can be suggested; null otherwise.</returns>
    Task<CommandProposal?> SuggestAsync(string input, CancellationToken cancellationToken = default);
}
