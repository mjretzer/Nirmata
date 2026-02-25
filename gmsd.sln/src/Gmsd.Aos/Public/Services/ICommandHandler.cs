using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Public.Models;

namespace Gmsd.Aos.Public.Services;

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
