using nirmata.Agents.Execution.Continuity.HistoryWriter;
using nirmata.Agents.Execution.Continuity.ProgressReporter;
using nirmata.Aos.Public.Models;
using nirmata.Aos.Public.Services;
using nirmata.Aos.Engine.Evidence;
using nirmata.Aos.Public.Catalogs;
using Microsoft.Extensions.DependencyInjection;

namespace nirmata.Agents.Configuration;

/// <summary>
/// Helper class to register continuity commands in the CommandCatalog.
/// </summary>
internal static class ContinuityCommandRegistrar
{
    /// <summary>
    /// Registers report-progress and write-history commands in the CommandCatalog.
    /// </summary>
    public static void Register(CommandCatalog catalog, IServiceProvider serviceProvider)
    {
        // Register progress reporter handler
        var progressReporter = serviceProvider.GetRequiredService<IProgressReporter>();
        var progressHandler = new ReportProgressCommandHandler(progressReporter);
        catalog.Register(progressHandler.Metadata, ctx => progressHandler.ExecuteAsync(ctx));

        // Register history writer handler
        var historyWriter = serviceProvider.GetRequiredService<IHistoryWriter>();
        var historyHandler = new WriteHistoryCommandHandler(historyWriter);
        catalog.Register(historyHandler.Metadata, ctx => historyHandler.ExecuteAsync(ctx));
    }
}
