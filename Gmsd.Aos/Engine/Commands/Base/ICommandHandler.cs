using Gmsd.Aos.Contracts.Commands;

namespace Gmsd.Aos.Engine.Commands.Base;

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
