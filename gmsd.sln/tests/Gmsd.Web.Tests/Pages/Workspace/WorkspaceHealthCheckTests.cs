using FluentAssertions;
using Gmsd.Web.Pages.Workspace;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Workspace;

public class WorkspaceHealthCheckTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;

    public WorkspaceHealthCheckTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        Directory.CreateDirectory(_aosDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { }
    }

    private void CreateDirectoryStructure(string[] directories, string[] files)
    {
        foreach (var dir in directories)
        {
            Directory.CreateDirectory(Path.Combine(_aosDir, dir));
        }
        foreach (var file in files)
        {
            var dir = Path.GetDirectoryName(Path.Combine(_aosDir, file));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(Path.Combine(_aosDir, file), "{}");
        }
    }

    private void CreateJsonFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_aosDir, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        File.WriteAllText(fullPath, content);
    }

    [Fact]
    public void PerformHealthCheck_WithMissingAosDirectory_ReturnsFailedReport()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var report = IndexModel.PerformHealthCheck(nonExistentPath);

        report.IsHealthy.Should().BeFalse();
        report.MissingDirectories.Should().Contain(".aos/ (directory not found)");
    }

    [Fact]
    public void PerformHealthCheck_WithAosAsFile_ReturnsFailedReport()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, ".aos"), "not a directory");

        try
        {
            var report = IndexModel.PerformHealthCheck(tempDir);

            report.IsHealthy.Should().BeFalse();
            report.MissingDirectories.Should().Contain(".aos/ (expected directory, found file)");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void PerformHealthCheck_WithAllRequiredDirectories_ReturnsHealthy()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        var files = new[]
        {
            "spec/project.json",
            "spec/roadmap.json",
            "spec/milestones/index.json",
            "spec/phases/index.json",
            "spec/tasks/index.json",
            "spec/issues/index.json",
            "spec/uat/index.json",
            "state/state.json",
            "state/events.ndjson",
            "evidence/logs/commands.json",
            "evidence/runs/index.json",
            "schemas/registry.json"
        };

        CreateDirectoryStructure(directories, files);
        CreateJsonFile("spec/project.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.IsHealthy.Should().BeTrue();
        report.MissingDirectories.Should().BeEmpty();
        report.MissingFiles.Should().BeEmpty();
        report.InvalidDirectories.Should().BeEmpty();
        report.InvalidFiles.Should().BeEmpty();
    }

    [Fact]
    public void PerformHealthCheck_WithMissingDirectories_ReportsMissing()
    {
        CreateJsonFile("spec/project.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.IsHealthy.Should().BeFalse();
        report.MissingDirectories.Should().Contain(".aos/state");
        report.MissingDirectories.Should().Contain(".aos/evidence");
        report.MissingDirectories.Should().Contain(".aos/context");
        report.MissingDirectories.Should().Contain(".aos/codebase");
        report.MissingDirectories.Should().Contain(".aos/cache");
        report.MissingDirectories.Should().Contain(".aos/config");
        report.MissingDirectories.Should().Contain(".aos/schemas");
        report.MissingDirectories.Should().Contain(".aos/locks");
    }

    [Fact]
    public void PerformHealthCheck_WithMissingFiles_ReportsMissing()
    {
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "evidence", "logs"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "evidence", "runs"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "schemas"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "context"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "codebase"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "cache"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "config"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "locks"));

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.IsHealthy.Should().BeFalse();
        report.MissingFiles.Should().Contain(".aos/spec/project.json");
        report.MissingFiles.Should().Contain(".aos/spec/roadmap.json");
        report.MissingFiles.Should().Contain(".aos/state/state.json");
    }

    [Fact]
    public void PerformHealthCheck_WithDirectoryAsFile_ReportsInvalid()
    {
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(Path.Combine(_aosDir, "spec"), "project.json"));
        Directory.CreateDirectory(Path.Combine(Path.Combine(_aosDir, "state"), "state.json"));

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.IsHealthy.Should().BeFalse();
        report.InvalidFiles.Should().Contain(".aos/spec/project.json (expected file, found directory)");
        report.InvalidFiles.Should().Contain(".aos/state/state.json (expected file, found directory)");
    }

    [Fact]
    public void PerformHealthCheck_WithExtraEntries_ReportsExtras()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        CreateDirectoryStructure(directories, Array.Empty<string>());
        Directory.CreateDirectory(Path.Combine(_aosDir, "extra-folder"));
        File.WriteAllText(Path.Combine(_aosDir, "extra-file.txt"), "extra");
        CreateJsonFile("spec/project.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.ExtraEntries.Should().Contain(".aos/extra-folder");
        report.ExtraEntries.Should().Contain(".aos/extra-file.txt");
    }

    [Fact]
    public void PerformHealthCheck_WithInvalidJson_ReportsInvalidFile()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        CreateDirectoryStructure(directories, Array.Empty<string>());
        CreateJsonFile("spec/project.json", "{invalid json}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.IsHealthy.Should().BeFalse();
        report.InvalidFiles.Should().Contain(".aos/spec/project.json (invalid JSON)");
    }

    [Fact]
    public void PerformHealthCheck_WithMissingSchemaVersion_ReportsSchemaError()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        CreateDirectoryStructure(directories, Array.Empty<string>());
        CreateJsonFile("spec/project.json", "{\"name\": \"test\"}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.IsHealthy.Should().BeFalse();
        report.SchemaValidationErrors.Should().Contain(".aos/spec/project.json (missing schemaVersion field)");
    }

    [Fact]
    public void PerformHealthCheck_WithPascalCaseSchemaVersion_DoesNotReportError()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        CreateDirectoryStructure(directories, Array.Empty<string>());
        CreateJsonFile("spec/project.json", "{\"SchemaVersion\": 1}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"SchemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.SchemaValidationErrors.Should().NotContain(e => e.Contains("project.json") && e.Contains("missing schemaVersion"));
        report.SchemaValidationErrors.Should().NotContain(e => e.Contains("state.json") && e.Contains("missing schemaVersion"));
    }

    [Fact]
    public void PerformHealthCheck_WithStringSchemaVersion_ReportsSchemaError()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        CreateDirectoryStructure(directories, Array.Empty<string>());
        CreateJsonFile("spec/project.json", "{\"schemaVersion\": \"1\"}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.SchemaValidationErrors.Should().Contain(".aos/spec/project.json (schemaVersion must be a number)");
    }

    [Fact]
    public void PerformHealthCheck_WithNoLockFile_ReturnsUnlocked()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        CreateDirectoryStructure(directories, Array.Empty<string>());
        CreateJsonFile("spec/project.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.LockStatus.Should().Be(LockStatus.Unlocked);
        report.LockInfo.Should().BeNull();
    }

    [Fact]
    public void PerformHealthCheck_WithValidLockFile_ReturnsLocked()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        CreateDirectoryStructure(directories, Array.Empty<string>());
        CreateJsonFile("spec/project.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");

        var lockContent = @"{ ""holder"": { ""command"": ""test-cmd"", ""pid"": 99999, ""machine"": ""test-pc"", ""user"": ""test-user"" }, ""acquiredAtUtc"": ""2024-01-01T00:00:00Z"" }";
        CreateJsonFile("locks/workspace.lock", lockContent);

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.LockStatus.Should().BeOneOf(LockStatus.LockedActive, LockStatus.LockedStale);
        report.LockInfo.Should().NotBeNull();
        report.LockInfo!.Command.Should().Be("test-cmd");
        report.LockInfo.Machine.Should().Be("test-pc");
        report.LockInfo.User.Should().Be("test-user");
    }

    [Fact]
    public void PerformHealthCheck_WithInvalidLockFile_ReturnsLockedUnknown()
    {
        var directories = new[] { "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks" };
        CreateDirectoryStructure(directories, Array.Empty<string>());
        CreateJsonFile("spec/project.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/roadmap.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/milestones/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/phases/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/tasks/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/issues/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("spec/uat/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/state.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("state/events.ndjson", "");
        CreateJsonFile("evidence/logs/commands.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("evidence/runs/index.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("schemas/registry.json", "{\"schemaVersion\": 1}");
        CreateJsonFile("locks/workspace.lock", "{invalid json}");

        var report = IndexModel.PerformHealthCheck(_tempDir);

        report.LockStatus.Should().Be(LockStatus.LockedUnknown);
    }

    [Fact]
    public void WorkspaceHealthReport_FromChecks_WithNoIssues_ReturnsHealthy()
    {
        var report = WorkspaceHealthReport.FromChecks(
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            LockStatus.Unlocked,
            null);

        report.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void WorkspaceHealthReport_FromChecks_WithMissingDirectory_ReturnsUnhealthy()
    {
        var report = WorkspaceHealthReport.FromChecks(
            new List<string> { ".aos/spec" },
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            LockStatus.Unlocked,
            null);

        report.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void WorkspaceHealthReport_FromChecks_WithSchemaError_ReturnsUnhealthy()
    {
        var report = WorkspaceHealthReport.FromChecks(
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string>(),
            new List<string> { ".aos/spec/project.json (invalid)" },
            LockStatus.Unlocked,
            null);

        report.IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void WorkspaceHealthReport_Failed_ReturnsUnhealthyWithProvidedData()
    {
        var report = WorkspaceHealthReport.Failed(
            new[] { ".aos/" },
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            LockStatus.Unknown,
            null);

        report.IsHealthy.Should().BeFalse();
        report.MissingDirectories.Should().Contain(".aos/");
        report.LockStatus.Should().Be(LockStatus.Unknown);
    }
}
