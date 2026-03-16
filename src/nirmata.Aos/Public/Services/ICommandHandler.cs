using nirmata.Aos.Contracts.Commands;
using nirmata.Aos.Public.Models;

namespace nirmata.Aos.Public.Services;

/// <summary>
/// Interface for command handlers.
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// The metadata for this command.
    /// </summary>
    CommandMetadata Metadata { get; }

    /// <summary>
    /// Executes the command.
    /// </summary>
    Task<CommandResult> ExecuteAsync(CommandContext context, CancellationToken ct = default);
}
