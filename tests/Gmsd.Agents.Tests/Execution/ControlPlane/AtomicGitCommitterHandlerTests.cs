using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.Execution.AtomicGitCommitter;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;
using Gmsd.Agents.Tests.Fakes;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane;

public class AtomicGitCommitterHandlerTests
{
    private readonly FakeAtomicGitCommitter _fakeCommitter = new();
    private readonly FakeWorkspace _workspace = new();
    private readonly FakeStateStore _stateStore = new();

    private AtomicGitCommitterHandler CreateHandler()
    {
        return new AtomicGitCommitterHandler(_fakeCommitter, _workspace, _stateStore);
    }

    [Fact]
    public async Task HandleAsync_SuccessPath_ReturnsSuccessAndRoutesToVerifier()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";
        var taskId = "TSK-0001";

        SetupTaskDirectory(taskId, ["src/**/*.cs"]);
        SetupStateSnapshot(taskId, "in_progress");

        _fakeCommitter.SetupCommitResult(new CommitResult
        {
            IsSuccess = true,
            CommitHash = "abc1234",
            DiffStat = new DiffStat { FilesChanged = 2, Insertions = 50, Deletions = 10 },
            FilesStaged = ["src/File1.cs", "src/File2.cs"]
        });

        var request = new CommandRequest
        {
            Group = "run",
            Command = "commit",
            Options = new Dictionary<string, string?>
            {
                ["task-id"] = taskId,
                ["summary"] = "Add new feature"
            }
        };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("abc1234", result.Output);
        Assert.Contains("2", result.Output); // Files staged count

        // Verify state was updated to route to Verifier
        var updatedState = _stateStore.ReadSnapshot();
        Assert.NotNull(updatedState);
        Assert.Equal("completed", updatedState.Cursor?.TaskStatus);
    }

    [Fact]
    public async Task HandleAsync_EmptyIntersection_ReturnsSuccessAndRoutesToVerifier()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";
        var taskId = "TSK-0001";

        // Setup scopes that don't match any changed files
        SetupTaskDirectory(taskId, ["tests/**/*.cs"]);
        SetupStateSnapshot(taskId, "in_progress");

        // No commit needed when intersection is empty
        _fakeCommitter.SetupCommitResult(new CommitResult
        {
            IsSuccess = true,
            CommitHash = null,
            DiffStat = new DiffStat { FilesChanged = 0, Insertions = 0, Deletions = 0 },
            FilesStaged = []
        });

        var request = new CommandRequest
        {
            Group = "run",
            Command = "commit",
            Options = new Dictionary<string, string?>
            {
                ["task-id"] = taskId
            }
        };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("No files to stage", result.Output);
        Assert.Contains("intersection empty", result.Output);

        // Verify state was updated to route to Verifier (empty intersection is not a failure)
        var updatedState = _stateStore.ReadSnapshot();
        Assert.NotNull(updatedState);
        Assert.Equal("completed", updatedState.Cursor?.TaskStatus);
    }

    [Fact]
    public async Task HandleAsync_GitError_ReturnsFailureAndRoutesToFixPlanner()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";
        var taskId = "TSK-0001";

        SetupTaskDirectory(taskId, ["src/**/*.cs"]);
        SetupStateSnapshot(taskId, "in_progress");

        _fakeCommitter.SetupCommitResult(new CommitResult
        {
            IsSuccess = false,
            CommitHash = null,
            DiffStat = null,
            ErrorMessage = "Git commit failed: nothing to commit, working tree clean"
        });

        var request = new CommandRequest
        {
            Group = "run",
            Command = "commit",
            Options = new Dictionary<string, string?>
            {
                ["task-id"] = taskId,
                ["summary"] = "Add new feature"
            }
        };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(5, result.ExitCode);
        Assert.Contains("Git commit failed", result.ErrorOutput);

        // Verify state was updated to route to FixPlanner
        var updatedState = _stateStore.ReadSnapshot();
        Assert.NotNull(updatedState);
        Assert.Equal("failed", updatedState.Cursor?.TaskStatus);
    }

    [Fact]
    public async Task HandleAsync_ScopeViolation_CommitsOnlyScopedFiles()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";
        var taskId = "TSK-0001";

        SetupTaskDirectory(taskId, ["src/**/*.cs"]);
        SetupStateSnapshot(taskId, "in_progress");

        // Simulate that only scoped files get committed (violation prevented by intersection)
        _fakeCommitter.SetupCommitResult(new CommitResult
        {
            IsSuccess = true,
            CommitHash = "def5678",
            DiffStat = new DiffStat { FilesChanged = 1, Insertions = 20, Deletions = 0 },
            FilesStaged = ["src/ScopedFile.cs"] // Only scoped file committed
        });

        var request = new CommandRequest
        {
            Group = "run",
            Command = "commit",
            Options = new Dictionary<string, string?>
            {
                ["task-id"] = taskId,
                ["summary"] = "Add scoped changes"
            }
        };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("def5678", result.Output);

        // Verify the commit only included scoped files (violation prevented)
        var commitResult = _fakeCommitter.LastCommitResult;
        Assert.NotNull(commitResult);
        Assert.Single(commitResult.FilesStaged);
        Assert.Contains("src/ScopedFile.cs", commitResult.FilesStaged);
    }

    [Fact]
    public async Task HandleAsync_MissingTaskId_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";

        var request = new CommandRequest { Group = "run", Command = "commit" }; // No task-id

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Task ID is required", result.ErrorOutput);
    }

    [Fact]
    public async Task HandleAsync_NoCursorTask_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";

        // Don't set up state - cursor will be null
        _stateStore.SetSnapshot(null);

        var request = new CommandRequest
        {
            Group = "run",
            Command = "commit",
            Options = new Dictionary<string, string?>
            {
                ["task-id"] = "TSK-0001"
            }
        };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("No current task found", result.ErrorOutput);
    }

    [Fact]
    public async Task HandleAsync_MissingTaskDirectory_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";
        var taskId = "TSK-0001";

        SetupStateSnapshot(taskId, "in_progress");
        // Don't create task directory

        var request = new CommandRequest
        {
            Group = "run",
            Command = "commit",
            Options = new Dictionary<string, string?>
            {
                ["task-id"] = taskId
            }
        };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(3, result.ExitCode);
        Assert.Contains("Task directory not found", result.ErrorOutput);
    }

    [Fact]
    public async Task HandleAsync_MissingPlanFile_ReturnsFailure()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "RUN-20260131211837-abc123";
        var taskId = "TSK-0001";

        SetupStateSnapshot(taskId, "in_progress");

        // Create task directory but not plan.json
        var taskDir = Path.Combine(_workspace.RepositoryRootPath, ".aos", "spec", "tasks", taskId);
        Directory.CreateDirectory(taskDir);

        var request = new CommandRequest
        {
            Group = "run",
            Command = "commit",
            Options = new Dictionary<string, string?>
            {
                ["task-id"] = taskId
            }
        };

        // Act
        var result = await handler.HandleAsync(request, runId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(4, result.ExitCode);
        Assert.Contains("Plan file not found", result.ErrorOutput);
    }

    private void SetupTaskDirectory(string taskId, string[] fileScopes)
    {
        var taskDir = Path.Combine(_workspace.RepositoryRootPath, ".aos", "spec", "tasks", taskId);
        Directory.CreateDirectory(taskDir);

        // Build JSON array of quoted scopes manually to avoid interpolation issues
        var scopesArray = string.Join(", ", fileScopes.Select(s => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""));
        var planContent = $$"""
            {
                "fileScopes": [{{scopesArray}}],
                "tasks": []
            }
            """;

        File.WriteAllText(Path.Combine(taskDir, "plan.json"), planContent);
    }

    private void SetupStateSnapshot(string taskId, string taskStatus)
    {
        var state = new StateSnapshot
        {
            SchemaVersion = 1,
            Cursor = new StateCursor
            {
                TaskId = taskId,
                TaskStatus = taskStatus,
                PhaseId = "PHASE-001",
                PhaseStatus = "in_progress",
                MilestoneId = "MILESTONE-001",
                MilestoneStatus = "in_progress"
            }
        };

        _stateStore.SetSnapshot(state);
    }
}
