using FluentAssertions;
using Gmsd.Web.Pages.Dashboard;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Gmsd.Web.Tests.Pages.Dashboard;

public class DashboardDataLoadingTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _aosDir;
    private readonly string _configDir;
    private readonly string _configFile;

    public DashboardDataLoadingTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _aosDir = Path.Combine(_tempDir, ".aos");
        _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Gmsd");
        _configFile = Path.Combine(_configDir, "workspace-config.json");
        Directory.CreateDirectory(_aosDir);
        Directory.CreateDirectory(_configDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
            if (File.Exists(_configFile))
            {
                File.Delete(_configFile);
            }
        }
        catch { }
    }

    private void CreateWorkspaceConfig(string workspacePath)
    {
        var config = $"{{ \"SelectedWorkspacePath\": \"{workspacePath.Replace("\\", "\\\\")}\", \"LastUpdated\": \"{DateTime.UtcNow:O}\" }}";
        File.WriteAllText(_configFile, config);
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

    private IndexModel CreateModel()
    {
        var configuration = new ConfigurationBuilder().Build();
        return new IndexModel(NullLogger<IndexModel>.Instance, configuration);
    }

    [Fact]
    public void OnGet_WithNoWorkspaceSelected_ShowsErrorMessage()
    {
        // Arrange
        if (File.Exists(_configFile))
        {
            File.Delete(_configFile);
        }
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.ErrorMessage.Should().NotBeNullOrEmpty();
        model.ErrorMessage.Should().Contain("No workspace selected");
        model.State.Should().BeNull();
    }

    [Fact]
    public void OnGet_WithInvalidAosDirectory_ShowsErrorMessage()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(invalidPath); // No .aos subdirectory
        CreateWorkspaceConfig(invalidPath);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.ErrorMessage.Should().NotBeNullOrEmpty();
        model.ErrorMessage.Should().Contain("does not have a valid .aos directory");
        model.State.Should().BeNull();

        // Cleanup
        Directory.Delete(invalidPath);
    }

    [Fact]
    public void OnGet_WithValidWorkspace_LoadsStateData()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "issues"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "evidence", "runs"));
        
        CreateJsonFile("state/state.json", @"{
            ""schemaVersion"": 1,
            ""status"": ""active"",
            ""cursor"": {
                ""milestoneId"": ""m1"",
                ""phaseId"": ""p1"",
                ""taskId"": ""t1"",
                ""stepId"": ""s1""
            }
        }");
        
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.ErrorMessage.Should().BeNull();
        model.State.Should().NotBeNull();
        model.State!.Status.Should().Be("active");
        model.State.MilestoneId.Should().Be("m1");
        model.State.PhaseId.Should().Be("p1");
        model.State.TaskId.Should().Be("t1");
        model.State.StepId.Should().Be("s1");
    }

    [Fact]
    public void OnGet_WithMissingStateFile_LeavesStateNull()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.State.Should().BeNull();
    }

    [Fact]
    public void OnGet_WithInvalidStateJson_LeavesStateNull()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateJsonFile("state/state.json", "{invalid json}");
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.State.Should().BeNull();
    }

    [Fact]
    public void OnGet_LoadsBlockersFromIssuesIndex()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "issues"));
        
        CreateJsonFile("spec/issues/index.json", @"{
            ""schemaVersion"": 1,
            ""items"": [
                { ""id"": ""ISS-001"", ""title"": ""Test Blocker"", ""status"": ""open"", ""severity"": ""high"", ""description"": ""Test description"" },
                { ""id"": ""ISS-002"", ""title"": ""Closed Issue"", ""status"": ""closed"", ""severity"": ""medium"" }
            ]
        }");
        
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.Blockers.Should().HaveCount(1);
        model.Blockers[0].Id.Should().Be("ISS-001");
        model.Blockers[0].Title.Should().Be("Test Blocker");
        model.Blockers[0].Severity.Should().Be("high");
        model.Blockers[0].Description.Should().Be("Test description");
    }

    [Fact]
    public void OnGet_LoadsBlockersFromIndividualIssueFiles()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "spec", "issues"));
        
        CreateJsonFile("spec/issues/blocking-issue.json", @"{
            ""id"": ""BLOCK-001"",
            ""title"": ""Blocking Issue"",
            ""status"": ""blocking"",
            ""severity"": ""critical""
        }");
        
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.Blockers.Should().Contain(b => b.Id == "BLOCK-001" && b.Severity == "critical");
    }

    [Fact]
    public void OnGet_WithNoBlockers_ShowsEmptyBlockerList()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.Blockers.Should().BeEmpty();
    }

    [Fact]
    public void OnGet_LoadsLatestRunFromRunsIndex()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        Directory.CreateDirectory(Path.Combine(_aosDir, "evidence", "runs"));
        
        CreateJsonFile("evidence/runs/index.json", @"{
            ""schemaVersion"": 1,
            ""items"": [
                { ""runId"": ""run-001"", ""status"": ""completed"", ""success"": true, ""startedAt"": ""2024-01-01T10:00:00Z"" },
                { ""runId"": ""run-002"", ""status"": ""completed"", ""success"": true, ""startedAt"": ""2024-01-01T11:00:00Z"", ""completedAt"": ""2024-01-01T11:05:00Z"" }
            ]
        }");
        
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.LatestRun.Should().NotBeNull();
        model.LatestRun!.RunId.Should().Be("run-002");
        model.LatestRun.Success.Should().BeTrue();
        model.LatestRun.StartedAt.Should().Be("2024-01-01T11:00:00Z");
        model.LatestRun.CompletedAt.Should().Be("2024-01-01T11:05:00Z");
    }

    [Fact]
    public void OnGet_WithNoRuns_ShowsNullLatestRun()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.LatestRun.Should().BeNull();
    }

    [Fact]
    public void OnGet_InitializesQuickActions()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        model.OnGet();

        // Assert
        model.QuickActions.Should().HaveCount(4);
        model.QuickActions.Should().Contain(a => a.Id == "validate" && a.Action == "validate");
        model.QuickActions.Should().Contain(a => a.Id == "checkpoint" && a.Action == "checkpoint");
        model.QuickActions.Should().Contain(a => a.Id == "pause" && a.Action == "pause");
        model.QuickActions.Should().Contain(a => a.Id == "resume" && a.Action == "resume");
    }

    [Fact]
    public void WorkspaceState_GetCursorDisplay_ReturnsFormattedPath()
    {
        // Arrange
        var state = new WorkspaceState
        {
            MilestoneId = "m1",
            PhaseId = "p1",
            TaskId = "t1",
            StepId = "s1"
        };

        // Act & Assert
        state.GetCursorDisplay().Should().Be("m1 / p1 / t1 / s1");
    }

    [Fact]
    public void WorkspaceState_GetCursorDisplay_WithPartialPath_ReturnsPartialPath()
    {
        // Arrange
        var state = new WorkspaceState
        {
            MilestoneId = "m1",
            PhaseId = "p1"
        };

        // Act & Assert
        state.GetCursorDisplay().Should().Be("m1 / p1");
    }

    [Fact]
    public void WorkspaceState_GetCursorDisplay_WithNoCursor_ReturnsDefaultMessage()
    {
        // Arrange
        var state = new WorkspaceState();

        // Act & Assert
        state.GetCursorDisplay().Should().Be("No cursor set");
    }

    [Theory]
    [InlineData("high", "severity-high")]
    [InlineData("critical", "severity-high")]
    [InlineData("medium", "severity-medium")]
    [InlineData("low", "severity-low")]
    [InlineData("unknown", "severity-medium")]
    public void Blocker_GetSeverityClass_ReturnsCorrectClass(string severity, string expectedClass)
    {
        // Arrange
        var blocker = new Blocker { Severity = severity };

        // Act & Assert
        blocker.GetSeverityClass().Should().Be(expectedClass);
    }

    [Theory]
    [InlineData(true, "success", "status-success")]
    [InlineData(false, "running", "status-running")]
    [InlineData(false, "failed", "status-error")]
    [InlineData(false, "unknown", "status-unknown")]
    public void RunSummary_GetStatusBadgeClass_ReturnsCorrectClass(bool success, string status, string expectedClass)
    {
        // Arrange
        var run = new RunSummary { Success = success, Status = status };

        // Act & Assert
        run.GetStatusBadgeClass().Should().Be(expectedClass);
    }

    [Fact]
    public void OnGetRefresh_ReturnsPartialView()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();

        // Act
        var result = model.OnGetRefresh();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.PartialViewResult>();
        var partialResult = (Microsoft.AspNetCore.Mvc.PartialViewResult)result;
        partialResult.ViewName.Should().Be("_DashboardContent");
        partialResult.Model.Should().Be(model);
    }

    [Fact]
    public void OnPostValidate_ReturnsPartialView()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();
        model.OnGet(); // Initialize data

        // Act
        var result = model.OnPostValidate();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.PartialViewResult>();
    }

    [Fact]
    public void OnPostCheckpoint_ReturnsPartialView()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();
        model.OnGet();

        // Act
        var result = model.OnPostCheckpoint();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.PartialViewResult>();
    }

    [Fact]
    public void OnPostPause_ReturnsPartialView()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();
        model.OnGet();

        // Act
        var result = model.OnPostPause();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.PartialViewResult>();
    }

    [Fact]
    public void OnPostResume_ReturnsPartialView()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(_aosDir, "state"));
        CreateWorkspaceConfig(_tempDir);
        var model = CreateModel();
        model.OnGet();

        // Act
        var result = model.OnPostResume();

        // Assert
        result.Should().BeOfType<Microsoft.AspNetCore.Mvc.PartialViewResult>();
    }
}
