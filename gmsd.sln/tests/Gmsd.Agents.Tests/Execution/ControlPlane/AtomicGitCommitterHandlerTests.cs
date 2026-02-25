using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Execution.Execution.AtomicGitCommitter;
using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;
using Gmsd.Agents.Tests.Fakes;
using System.Text.Json;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.ControlPlane;

public class AtomicGitCommitterHandlerTests
{
    private readonly FakeAtomicGitCommitter _fakeCommitter = new();
    private readonly FakeWorkspace _workspace = new();
    private readonly FakeStateStore _stateStore;

    public AtomicGitCommitterHandlerTests()
    {
        _stateStore = new FakeStateStore(_workspace.RepositoryRootPath);
        InitializeGitRepo();
    }

    private void InitializeGitRepo()
    {
        // Initialize git repo
        var gitRunner = new GitCommandRunner(_workspace.RepositoryRootPath);
        gitRunner.ExecuteAsync("init", CancellationToken.None).Wait();
        gitRunner.ExecuteAsync("config user.email 'test@example.com'", CancellationToken.None).Wait();
        gitRunner.ExecuteAsync("config user.name 'Test User'", CancellationToken.None).Wait();
    }

    private AtomicGitCommitterHandler CreateHandler()
    {
        return new AtomicGitCommitterHandler(_fakeCommitter, _workspace, _stateStore);
    }

    private StateSnapshot? ReadStateFromFile()
    {
        var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
        if (!File.Exists(statePath)) return null;
        var json = File.ReadAllText(statePath);
        return JsonSerializer.Deserialize<StateSnapshot>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }

    [Fact]
    public async Task HandleAsync_SuccessPath_ReturnsSuccessAndRoutesToVerifier()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "3d76858928df48de9902dca9a8742009"; // Valid 32-char hex run ID
        var taskId = "TSK-000001";

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
        var updatedState = ReadStateFromFile();
        Assert.NotNull(updatedState);
        Assert.Equal("completed", updatedState.Cursor?.TaskStatus);
    }

    [Fact]
    public async Task HandleAsync_EmptyIntersection_ReturnsSuccessAndRoutesToVerifier()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "3d76858928df48de9902dca9a8742009";
        var taskId = "TSK-000001";

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
        var updatedState = ReadStateFromFile();
        Assert.NotNull(updatedState);
        Assert.Equal("completed", updatedState.Cursor?.TaskStatus);
    }

    [Fact]
    public async Task HandleAsync_GitError_ReturnsFailureAndRoutesToFixPlanner()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "3d76858928df48de9902dca9a8742009";
        var taskId = "TSK-000001";

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
        var updatedState = ReadStateFromFile();
        Assert.NotNull(updatedState);
        Assert.Equal("failed", updatedState.Cursor?.TaskStatus);
    }

    [Fact]
    public async Task HandleAsync_ScopeViolation_CommitsOnlyScopedFiles()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "3d76858928df48de9902dca9a8742009";
        var taskId = "TSK-000001";

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
        Assert.True(result.IsSuccess, $"Handler failed with output: {result.Output} | Error: {result.ErrorOutput}");
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
        var runId = "3d76858928df48de9902dca9a8742009";

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
                ["task-id"] = "TSK-000001"
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
        var runId = "3d76858928df48de9902dca9a8742009";
        var taskId = "TSK-000001";

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
        var runId = "3d76858928df48de9902dca9a8742009";
        var taskId = "TSK-000001";

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

    [Fact]
    public async Task HandleAsync_InvalidPlanContract_FailsFastAndSkipsCommit()
    {
        // Arrange
        var handler = CreateHandler();
        var runId = "3d76858928df48de9902dca9a8742009";
        var taskId = "TSK-000001";

        SetupStateSnapshot(taskId, "in_progress");

        var taskDir = Path.Combine(_workspace.RepositoryRootPath, ".aos", "spec", "tasks", taskId);
        Directory.CreateDirectory(taskDir);
        File.WriteAllText(Path.Combine(taskDir, "plan.json"), "{\"fileScopes\":[\"src/File1.cs\"]}");

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
        Assert.Equal(6, result.ExitCode);
        Assert.Contains("contract validation gate", result.ErrorOutput);
        Assert.Contains("Diagnostic:", result.ErrorOutput);
        Assert.Null(_fakeCommitter.LastCommitRequest);

        var updatedState = ReadStateFromFile();
        Assert.NotNull(updatedState);
        Assert.Equal("in_progress", updatedState.Cursor?.TaskStatus);
    }

    private void SetupTaskDirectory(string taskId, string[] fileScopes)
    {
        var taskDir = Path.Combine(_workspace.RepositoryRootPath, ".aos", "spec", "tasks", taskId);
        Directory.CreateDirectory(taskDir);

        // Build canonical task-plan fileScopes object entries.
        var scopesArray = string.Join(", ", fileScopes.Select(s => "{\"path\":\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\"scopeType\":\"modify\"}"));
        var planContent = $$"""
            {
                "schemaVersion": 1,
                "taskId": "{{taskId}}",
                "title": "Test Task",
                "description": "Test description",
                "fileScopes": [{{scopesArray}}],
                "steps": []
            }
            """;

        File.WriteAllText(Path.Combine(taskDir, "plan.json"), planContent);

        // Create dummy files for scopes to ensure git status picks them up
        foreach (var scope in fileScopes)
        {
            // Simple handling for glob patterns: assume "src/**/*.cs" -> create "src/File1.cs" and "src/File2.cs"
            if (scope.Contains("**"))
            {
                var dir = Path.Combine(_workspace.RepositoryRootPath, "src");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "File1.cs"), "// Test file 1");
                File.WriteAllText(Path.Combine(dir, "File2.cs"), "// Test file 2");
                
                // Special case for scoped file test
                File.WriteAllText(Path.Combine(dir, "ScopedFile.cs"), "// Scoped file");
            }
            else
            {
                // Exact path
                var path = Path.Combine(_workspace.RepositoryRootPath, scope);
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(path, "// Test file");
            }
        }
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
                PhaseId = "PH-0001",
                PhaseStatus = "in_progress",
                MilestoneId = "MS-0001",
                MilestoneStatus = "in_progress"
            }
        };

        _stateStore.SetSnapshot(state);

        // Write to disk so the handler can read/update it
        var statePath = Path.Combine(_workspace.AosRootPath, "state", "state.json");
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(statePath, json);
    }
}
