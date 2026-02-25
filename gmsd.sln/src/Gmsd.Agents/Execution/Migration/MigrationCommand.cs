using Gmsd.Aos.Public;

namespace Gmsd.Agents.Execution.Migration;

/// <summary>
/// CLI command for migrating artifacts from old format to new canonical schemas.
/// </summary>
public sealed class MigrationCommand
{
    private readonly IWorkspace _workspace;

    public MigrationCommand(IWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <summary>
    /// Executes the schema migration process.
    /// </summary>
    public async Task ExecuteAsync(string workspacePath, bool dryRun = false, bool backup = true)
    {
        await ExecuteMigrationAsync(workspacePath, dryRun, backup);
    }

    private async Task ExecuteMigrationAsync(string workspacePath, bool dryRun, bool backup)
    {
        try
        {
            Console.WriteLine("Starting schema migration...");
            Console.WriteLine($"Workspace: {workspacePath}");
            Console.WriteLine($"Dry Run: {dryRun}");
            Console.WriteLine($"Backup: {backup}");
            Console.WriteLine();

            var migrator = new SchemaMigrator(_workspace);

            // Create backup if requested
            string? backupPath = null;
            if (backup && !dryRun)
            {
                Console.WriteLine("Creating workspace backup...");
                backupPath = await migrator.CreateWorkspaceBackupAsync(CancellationToken.None);
                Console.WriteLine($"Backup created at: {backupPath}");
                Console.WriteLine();
            }

            // Discover artifacts requiring migration
            Console.WriteLine("Discovering artifacts requiring migration...");
            var artifactsToMigrate = await migrator.DiscoverArtifactsRequiringMigrationAsync(CancellationToken.None);
            Console.WriteLine($"Found {artifactsToMigrate.Count} artifacts requiring migration");
            Console.WriteLine();

            if (artifactsToMigrate.Count == 0)
            {
                Console.WriteLine("No artifacts requiring migration found.");
                return;
            }

            // Display artifacts to be migrated
            Console.WriteLine("Artifacts to migrate:");
            foreach (var artifact in artifactsToMigrate)
            {
                Console.WriteLine($"  - {artifact.ArtifactType}: {artifact.ArtifactPath}");
            }
            Console.WriteLine();

            // Perform migration
            Console.WriteLine(dryRun ? "Performing dry-run migration..." : "Performing migration...");
            var successCount = 0;
            var failureCount = 0;

            foreach (var artifact in artifactsToMigrate)
            {
                var result = await migrator.MigrateArtifactAsync(artifact, dryRun, CancellationToken.None);

                if (result.IsSuccess)
                {
                    successCount++;
                    Console.WriteLine($"  ✓ {artifact.ArtifactPath}");
                }
                else
                {
                    failureCount++;
                    Console.WriteLine($"  ✗ {artifact.ArtifactPath}: {result.ErrorMessage}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Migration complete: {successCount} succeeded, {failureCount} failed");

            if (failureCount > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Some artifacts failed to migrate. Please review the errors above.");
                if (backupPath != null)
                {
                    Console.WriteLine($"Backup is available at: {backupPath}");
                }
            }
            else
            {
                if (dryRun)
                {
                    Console.WriteLine("Dry-run completed successfully. Run without --dry-run to apply changes.");
                }
                else
                {
                    Console.WriteLine("Migration completed successfully!");
                    if (backupPath != null)
                    {
                        Console.WriteLine($"Original artifacts backed up at: {backupPath}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Migration failed: {ex.Message}");
        }
    }
}
