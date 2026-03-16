using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Agents.Execution.Migration;
using nirmata.Aos.Public;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Migration;

public sealed class SchemaMigratorIntegrationTests : IAsyncLifetime
{
    private readonly string _testWorkspaceDir;
    private readonly IWorkspace _workspace;

    public SchemaMigratorIntegrationTests()
    {
        _testWorkspaceDir = Path.Combine(Path.GetTempPath(), $"nirmata-migration-test-{Guid.NewGuid()}");
        _workspace = new TestWorkspace(_testWorkspaceDir);
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_testWorkspaceDir);
        await SetupTestWorkspaceAsync();
    }

    public async Task DisposeAsync()
    {
        if (Directory.Exists(_testWorkspaceDir))
        {
            await Task.Run(() => Directory.Delete(_testWorkspaceDir, recursive: true));
        }
    }

    private async Task SetupTestWorkspaceAsync()
    {
        // Create old format task plan
        var taskPlanDir = Path.Combine(_testWorkspaceDir, ".aos", "spec", "tasks", "TSK-0001");
        Directory.CreateDirectory(taskPlanDir);

        var oldTaskPlan = """
        {
          "taskId": "TSK-0001",
          "fileScopes": ["src/", "tests/"],
          "verificationSteps": [
            {
              "id": "V1",
              "description": "Check files exist"
            }
          ]
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(taskPlanDir, "plan.json"), oldTaskPlan);

        // Create old format verifier output
        var evidenceDir = Path.Combine(_testWorkspaceDir, ".aos", "evidence", "runs", "RUN-0001", "artifacts");
        Directory.CreateDirectory(evidenceDir);

        var oldVerifierOutput = """
        {
          "taskId": "TSK-0001",
          "runId": "RUN-0001",
          "status": "passed",
          "checks": [
            {
              "criterionId": "C1",
              "passed": true,
              "message": "Check passed"
            }
          ]
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(evidenceDir, "uat-results.json"), oldVerifierOutput);

        // Create old format fix plan
        var fixesDir = Path.Combine(_testWorkspaceDir, ".aos", "spec", "fixes", "FIX-0001");
        Directory.CreateDirectory(fixesDir);

        var oldFixPlan = """
        {
          "taskId": "TSK-0001",
          "fixes": [
            {
              "id": "F1",
              "description": "Fix issue"
            }
          ],
          "verificationSteps": []
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(fixesDir, "plan.json"), oldFixPlan);
    }

    [Fact]
    public async Task DiscoverArtifactsRequiringMigration_FindsAllOldFormatArtifacts()
    {
        // Arrange
        var migrator = new SchemaMigrator(_workspace);

        // Act
        var artifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();

        // Assert
        Assert.NotEmpty(artifacts);
        Assert.True(artifacts.Count >= 3, $"Expected at least 3 artifacts, found {artifacts.Count}");
        
        var taskPlanArtifact = artifacts.FirstOrDefault(a => a.ArtifactType == ArtifactType.TaskPlan);
        Assert.NotNull(taskPlanArtifact);
        Assert.True(taskPlanArtifact.RequiresMigration);

        var verifierOutputArtifact = artifacts.FirstOrDefault(a => a.ArtifactType == ArtifactType.VerifierOutput);
        Assert.NotNull(verifierOutputArtifact);
        Assert.True(verifierOutputArtifact.RequiresMigration);

        var fixPlanArtifact = artifacts.FirstOrDefault(a => a.ArtifactType == ArtifactType.FixPlan);
        Assert.NotNull(fixPlanArtifact);
        Assert.True(fixPlanArtifact.RequiresMigration);
    }

    [Fact]
    public async Task MigrateArtifactAsync_TransformsTaskPlanSuccessfully()
    {
        // Arrange
        var migrator = new SchemaMigrator(_workspace);
        var artifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();
        var taskPlanArtifact = artifacts.First(a => a.ArtifactType == ArtifactType.TaskPlan);

        // Act
        var result = await migrator.MigrateArtifactAsync(taskPlanArtifact, dryRun: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.WasDryRun);

        // Verify transformed artifact
        var transformedJson = await File.ReadAllTextAsync(taskPlanArtifact.ArtifactPath);
        using var doc = JsonDocument.Parse(transformedJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaVersion", out var schemaVersion));
        Assert.Equal(1, schemaVersion.GetInt32());

        Assert.True(root.TryGetProperty("schemaId", out var schemaId));
        Assert.Equal("nirmata:aos:schema:task-plan:v1", schemaId.GetString());

        Assert.True(root.TryGetProperty("fileScopes", out var fileScopes));
        var scopes = fileScopes.EnumerateArray().ToList();
        Assert.Equal(2, scopes.Count);
        Assert.Equal("src/", scopes[0].GetProperty("path").GetString());
        Assert.Equal("tests/", scopes[1].GetProperty("path").GetString());
    }

    [Fact]
    public async Task MigrateArtifactAsync_DryRunDoesNotModifyFiles()
    {
        // Arrange
        var migrator = new SchemaMigrator(_workspace);
        var artifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();
        var taskPlanArtifact = artifacts.First(a => a.ArtifactType == ArtifactType.TaskPlan);

        var originalContent = await File.ReadAllTextAsync(taskPlanArtifact.ArtifactPath);

        // Act
        var result = await migrator.MigrateArtifactAsync(taskPlanArtifact, dryRun: true);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.WasDryRun);

        // Verify file was not modified
        var currentContent = await File.ReadAllTextAsync(taskPlanArtifact.ArtifactPath);
        Assert.Equal(originalContent, currentContent);
    }

    [Fact]
    public async Task MigrateArtifactAsync_CreatesBackupFile()
    {
        // Arrange
        var migrator = new SchemaMigrator(_workspace);
        var artifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();
        var taskPlanArtifact = artifacts.First(a => a.ArtifactType == ArtifactType.TaskPlan);

        var backupPath = taskPlanArtifact.ArtifactPath + ".backup";
        Assert.False(File.Exists(backupPath));

        // Act
        var result = await migrator.MigrateArtifactAsync(taskPlanArtifact, dryRun: false);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(backupPath));

        // Verify backup contains original content
        var backupContent = await File.ReadAllTextAsync(backupPath);
        using var doc = JsonDocument.Parse(backupContent);
        var root = doc.RootElement;

        // Original format should not have schemaVersion
        Assert.False(root.TryGetProperty("schemaVersion", out _));
    }

    [Fact]
    public async Task MigrateArtifactAsync_TransformsVerifierOutputSuccessfully()
    {
        // Arrange
        var migrator = new SchemaMigrator(_workspace);
        var artifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();
        var verifierOutputArtifact = artifacts.First(a => a.ArtifactType == ArtifactType.VerifierOutput);

        // Act
        var result = await migrator.MigrateArtifactAsync(verifierOutputArtifact, dryRun: false);

        // Assert
        Assert.True(result.IsSuccess);

        // Verify transformed artifact
        var transformedJson = await File.ReadAllTextAsync(verifierOutputArtifact.ArtifactPath);
        using var doc = JsonDocument.Parse(transformedJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaId", out var schemaId));
        Assert.Equal("nirmata:aos:schema:verifier-output:v1", schemaId.GetString());

        Assert.True(root.TryGetProperty("status", out var status));
        Assert.Equal("passed", status.GetString());
    }

    [Fact]
    public async Task MigrateArtifactAsync_TransformsFixPlanSuccessfully()
    {
        // Arrange
        var migrator = new SchemaMigrator(_workspace);
        var artifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();
        var fixPlanArtifact = artifacts.First(a => a.ArtifactType == ArtifactType.FixPlan);

        // Act
        var result = await migrator.MigrateArtifactAsync(fixPlanArtifact, dryRun: false);

        // Assert
        Assert.True(result.IsSuccess);

        // Verify transformed artifact
        var transformedJson = await File.ReadAllTextAsync(fixPlanArtifact.ArtifactPath);
        using var doc = JsonDocument.Parse(transformedJson);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("schemaId", out var schemaId));
        Assert.Equal("nirmata:aos:schema:fix-plan:v1", schemaId.GetString());
    }

    [Fact]
    public async Task CreateWorkspaceBackupAsync_CreatesBackupDirectory()
    {
        // Arrange
        var migrator = new SchemaMigrator(_workspace);

        // Act
        var backupPath = await migrator.CreateWorkspaceBackupAsync();

        // Assert
        Assert.True(Directory.Exists(backupPath));
        Assert.True(Directory.Exists(Path.Combine(backupPath, "spec")));
        Assert.True(Directory.Exists(Path.Combine(backupPath, "evidence")));
    }

    [Fact]
    public async Task RestoreFromBackupAsync_RestoresWorkspaceState()
    {
        // Arrange
        var migrator = new SchemaMigrator(_workspace);
        var backupPath = await migrator.CreateWorkspaceBackupAsync();

        // Migrate artifacts
        var artifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();
        foreach (var artifact in artifacts)
        {
            await migrator.MigrateArtifactAsync(artifact, dryRun: false);
        }

        // Verify artifacts were migrated
        var migratedArtifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();
        Assert.Empty(migratedArtifacts);

        // Act
        await migrator.RestoreFromBackupAsync(backupPath);

        // Assert
        var restoredArtifacts = await migrator.DiscoverArtifactsRequiringMigrationAsync();
        Assert.NotEmpty(restoredArtifacts);
        Assert.Equal(artifacts.Count, restoredArtifacts.Count);
    }

    /// <summary>
    /// Test workspace implementation for testing.
    /// </summary>
    private sealed class TestWorkspace : IWorkspace
    {
        private readonly string _rootPath;

        public TestWorkspace(string rootPath)
        {
            _rootPath = rootPath;
        }

        public string RepositoryRootPath => _rootPath;
        public string AosRootPath => Path.Combine(_rootPath, ".aos");
        public string SpecRootPath => Path.Combine(AosRootPath, "spec");
        public string EvidenceRootPath => Path.Combine(AosRootPath, "evidence");
        public string DiagnosticsRootPath => Path.Combine(AosRootPath, "diagnostics");

        public string GetContractPathForArtifactId(string artifactId) => Path.Combine(AosRootPath, "contracts", $"{artifactId}.json");
        public string GetAbsolutePathForContractPath(string contractPath) => Path.Combine(_rootPath, contractPath);
        public string GetAbsolutePathForArtifactId(string artifactId) => Path.Combine(AosRootPath, "artifacts", $"{artifactId}.json");
        public JsonElement ReadArtifact(string artifactId, string schemaVersion) => JsonDocument.Parse("{}").RootElement;
    }
}
