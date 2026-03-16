using System.Text.Json;
using nirmata.Aos.Public;

namespace nirmata.Agents.Execution.Migration;

/// <summary>
/// Orchestrates migration of artifacts from old format to new canonical schemas.
/// </summary>
public sealed class SchemaMigrator
{
    private readonly IWorkspace _workspace;
    private readonly string _aosRootPath;

    /// <summary>
    /// Initializes a new instance of the <see cref="SchemaMigrator"/> class.
    /// </summary>
    public SchemaMigrator(IWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
        _aosRootPath = workspace.AosRootPath;
    }

    /// <summary>
    /// Discovers all artifacts in the workspace that require migration.
    /// </summary>
    public async Task<IReadOnlyList<ArtifactFormatInfo>> DiscoverArtifactsRequiringMigrationAsync(CancellationToken ct = default)
    {
        var artifactsToMigrate = new List<ArtifactFormatInfo>();

        // Discover task plans
        var taskPlansDir = Path.Combine(_aosRootPath, "spec", "tasks");
        if (Directory.Exists(taskPlansDir))
        {
            foreach (var planFile in Directory.EnumerateFiles(taskPlansDir, "plan.json", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(planFile, ct);
                var formatInfo = ArtifactFormatDetector.DetectFormat(planFile, content);
                if (formatInfo.RequiresMigration)
                {
                    artifactsToMigrate.Add(formatInfo);
                }
            }
        }

        // Discover verifier inputs
        var verifierInputsDir = Path.Combine(_aosRootPath, "spec", "uat");
        if (Directory.Exists(verifierInputsDir))
        {
            foreach (var inputFile in Directory.EnumerateFiles(verifierInputsDir, "*.json", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(inputFile, ct);
                var formatInfo = ArtifactFormatDetector.DetectFormat(inputFile, content);
                if (formatInfo.RequiresMigration)
                {
                    artifactsToMigrate.Add(formatInfo);
                }
            }
        }

        // Discover verifier outputs
        var evidenceDir = Path.Combine(_aosRootPath, "evidence");
        if (Directory.Exists(evidenceDir))
        {
            foreach (var resultFile in Directory.EnumerateFiles(evidenceDir, "uat-results.json", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(resultFile, ct);
                var formatInfo = ArtifactFormatDetector.DetectFormat(resultFile, content);
                if (formatInfo.RequiresMigration)
                {
                    artifactsToMigrate.Add(formatInfo);
                }
            }
        }

        // Discover fix plans
        var fixesDir = Path.Combine(_aosRootPath, "spec", "fixes");
        if (Directory.Exists(fixesDir))
        {
            foreach (var fixPlanFile in Directory.EnumerateFiles(fixesDir, "plan.json", SearchOption.AllDirectories))
            {
                var content = await File.ReadAllTextAsync(fixPlanFile, ct);
                var formatInfo = ArtifactFormatDetector.DetectFormat(fixPlanFile, content);
                if (formatInfo.RequiresMigration)
                {
                    artifactsToMigrate.Add(formatInfo);
                }
            }
        }

        return artifactsToMigrate.AsReadOnly();
    }

    /// <summary>
    /// Migrates a single artifact to the new canonical format.
    /// </summary>
    public async Task<MigrationResult> MigrateArtifactAsync(
        ArtifactFormatInfo formatInfo,
        bool dryRun = false,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(formatInfo);

        try
        {
            var originalContent = await File.ReadAllTextAsync(formatInfo.ArtifactPath, ct);
            var transformedContent = ArtifactTransformer.TransformToNewFormat(originalContent, formatInfo.ArtifactType);

            // Validate transformed artifact
            var validationResult = ValidateTransformedArtifact(transformedContent, formatInfo.ArtifactType);
            if (!validationResult.IsValid)
            {
                return MigrationResult.Failure(
                    formatInfo.ArtifactPath,
                    $"Transformed artifact failed validation: {validationResult.ErrorMessage}");
            }

            if (!dryRun)
            {
                // Create backup of original
                var backupPath = formatInfo.ArtifactPath + ".backup";
                await File.WriteAllTextAsync(backupPath, originalContent, ct);

                // Write transformed artifact
                await File.WriteAllTextAsync(formatInfo.ArtifactPath, transformedContent, ct);
            }

            return MigrationResult.Success(formatInfo.ArtifactPath, dryRun);
        }
        catch (Exception ex)
        {
            return MigrationResult.Failure(formatInfo.ArtifactPath, ex.Message);
        }
    }

    /// <summary>
    /// Validates a transformed artifact against canonical schema.
    /// </summary>
    private static ValidationResult ValidateTransformedArtifact(string artifactJson, ArtifactType artifactType)
    {
        try
        {
            using var doc = JsonDocument.Parse(artifactJson);
            var root = doc.RootElement;

            // Check for required schema fields
            if (!root.TryGetProperty("schemaVersion", out _))
                return ValidationResult.Failure("Missing schemaVersion field");

            if (!root.TryGetProperty("schemaId", out _))
                return ValidationResult.Failure("Missing schemaId field");

            // Type-specific validation
            return artifactType switch
            {
                ArtifactType.TaskPlan => ValidateTaskPlanStructure(root),
                ArtifactType.VerifierInput => ValidateVerifierInputStructure(root),
                ArtifactType.VerifierOutput => ValidateVerifierOutputStructure(root),
                ArtifactType.FixPlan => ValidateFixPlanStructure(root),
                ArtifactType.PhasePlan => ValidatePhasePlanStructure(root),
                _ => ValidationResult.Success()
            };
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"JSON parsing error: {ex.Message}");
        }
    }

    private static ValidationResult ValidateTaskPlanStructure(JsonElement root)
    {
        if (!root.TryGetProperty("fileScopes", out var fileScopes))
            return ValidationResult.Failure("Missing fileScopes field");

        if (fileScopes.ValueKind != JsonValueKind.Array)
            return ValidationResult.Failure("fileScopes must be an array");

        foreach (var scope in fileScopes.EnumerateArray())
        {
            if (scope.ValueKind != JsonValueKind.Object)
                return ValidationResult.Failure("Each fileScope must be an object");

            if (!scope.TryGetProperty("path", out _))
                return ValidationResult.Failure("Each fileScope must have a 'path' field");
        }

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateVerifierInputStructure(JsonElement root)
    {
        if (!root.TryGetProperty("acceptanceCriteria", out _))
            return ValidationResult.Failure("Missing acceptanceCriteria field");

        if (!root.TryGetProperty("fileScopes", out _))
            return ValidationResult.Failure("Missing fileScopes field");

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateVerifierOutputStructure(JsonElement root)
    {
        if (!root.TryGetProperty("status", out _))
            return ValidationResult.Failure("Missing status field");

        if (!root.TryGetProperty("checks", out _))
            return ValidationResult.Failure("Missing checks field");

        return ValidationResult.Success();
    }

    private static ValidationResult ValidateFixPlanStructure(JsonElement root)
    {
        if (!root.TryGetProperty("fixes", out _))
            return ValidationResult.Failure("Missing fixes field");

        return ValidationResult.Success();
    }

    private static ValidationResult ValidatePhasePlanStructure(JsonElement root)
    {
        if (!root.TryGetProperty("tasks", out _))
            return ValidationResult.Failure("Missing tasks field");

        return ValidationResult.Success();
    }

    /// <summary>
    /// Creates a backup of the entire workspace before migration.
    /// </summary>
    public async Task<string> CreateWorkspaceBackupAsync(CancellationToken ct = default)
    {
        var backupDir = Path.Combine(
            Path.GetDirectoryName(_aosRootPath) ?? "",
            $"backup-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}");

        Directory.CreateDirectory(backupDir);

        // Copy entire .aos directory
        await CopyDirectoryAsync(_aosRootPath, backupDir, ct);

        return backupDir;
    }

    /// <summary>
    /// Restores workspace from a backup.
    /// </summary>
    public async Task RestoreFromBackupAsync(string backupPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        if (!Directory.Exists(backupPath))
            throw new DirectoryNotFoundException($"Backup directory not found: {backupPath}");

        // Remove current .aos directory
        if (Directory.Exists(_aosRootPath))
        {
            Directory.Delete(_aosRootPath, recursive: true);
        }

        // Restore from backup
        await CopyDirectoryAsync(backupPath, _aosRootPath, ct);
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            await Task.Run(() => File.Copy(file, destFile, overwrite: true), ct);
        }

        foreach (var subDir in Directory.EnumerateDirectories(sourceDir))
        {
            var destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
            await CopyDirectoryAsync(subDir, destSubDir, ct);
        }
    }
}

/// <summary>
/// Result of a migration operation.
/// </summary>
public sealed record MigrationResult
{
    /// <summary>
    /// Path to the migrated artifact.
    /// </summary>
    public required string ArtifactPath { get; init; }

    /// <summary>
    /// Whether the migration succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if migration failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether this was a dry-run operation.
    /// </summary>
    public required bool WasDryRun { get; init; }

    public static MigrationResult Success(string artifactPath, bool wasDryRun)
        => new() { ArtifactPath = artifactPath, IsSuccess = true, WasDryRun = wasDryRun };

    public static MigrationResult Failure(string artifactPath, string errorMessage)
        => new() { ArtifactPath = artifactPath, IsSuccess = false, ErrorMessage = errorMessage, WasDryRun = false };
}

/// <summary>
/// Result of artifact validation.
/// </summary>
internal sealed record ValidationResult
{
    public required bool IsValid { get; init; }
    public string? ErrorMessage { get; init; }

    public static ValidationResult Success() => new() { IsValid = true };
    public static ValidationResult Failure(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
}
