using nirmata.Data.Dto.Models.Workspaces;

namespace nirmata.Services.Interfaces;

/// <summary>
/// Bootstraps a workspace root by ensuring a valid git repository exists
/// and seeding the required AOS directory scaffold.
/// </summary>
public interface IWorkspaceBootstrapService
{
    /// <summary>
    /// Bootstraps the workspace at <paramref name="path"/>.
    /// <list type="bullet">
    ///   <item>If <c>.git/</c> is absent, runs <c>git init</c>.</item>
    ///   <item>If <c>.aos/</c> or any required subdirectory is absent, creates it.</item>
    ///   <item>Idempotent: safe to call on an already-bootstrapped workspace.</item>
    /// </list>
    /// </summary>
    /// <param name="path">Absolute path to the workspace root directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A <see cref="WorkspaceBootstrapResult"/> describing whether bootstrap succeeded,
    /// what was created, and the failure reason if it did not.
    /// </returns>
    Task<WorkspaceBootstrapResult> BootstrapAsync(string path, CancellationToken cancellationToken = default, string? remoteUrl = null);
}
