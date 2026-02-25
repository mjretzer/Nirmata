using System.Text.Json;
using AtomicGitCommitterAlias = Gmsd.Agents.Execution.Execution.AtomicGitCommitter.AtomicGitCommitter;
using Gmsd.Agents.Execution.Execution.AtomicGitCommitter;
using Gmsd.Aos.Engine.Paths;
using Gmsd.Aos.Public;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Gmsd.Agents.Tests.Execution.AtomicGitCommitter;

/// <summary>
/// Integration tests for AtomicGitCommitter that use real git repositories.
/// All tests use temporary directories and do not depend on real repositories.
/// </summary>
public class AtomicGitCommitterIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _repoPath;
    private readonly GitCommandRunner _gitRunner;
    private readonly ILogger<AtomicGitCommitterAlias> _logger;
    private readonly ITestOutputHelper _output;

    public AtomicGitCommitterIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _tempDir = Path.Combine(Path.GetTempPath(), $"atomic-git-committer-test-{Guid.NewGuid():N}");
        _repoPath = Path.Combine(_tempDir, "repo");
        Directory.CreateDirectory(_repoPath);
        _gitRunner = new GitCommandRunner(_repoPath);
        _logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<AtomicGitCommitterAlias>();
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
        catch
        {
            // Best effort cleanup
        }
    }

    /// <summary>
    /// Creates a test workspace with the AOS structure.
    /// </summary>
    private TestWorkspace CreateTestWorkspace()
    {
        var workspace = new TestWorkspace(_repoPath);
        Directory.CreateDirectory(Path.Combine(_repoPath, ".aos", "evidence"));
        Directory.CreateDirectory(Path.Combine(_repoPath, ".aos", "state"));
        return workspace;
    }

    /// <summary>
    /// Initializes a git repository with user config.
    /// </summary>
    private async Task InitializeGitRepoAsync()
    {
        var initResult = await _gitRunner.ExecuteAsync("init");
        Assert.True(initResult.IsSuccess, $"Git init failed: {initResult.Stderr}");

        var emailResult = await _gitRunner.ExecuteAsync("config user.email \"test@test.com\"");
        Assert.True(emailResult.IsSuccess);

        var nameResult = await _gitRunner.ExecuteAsync("config user.name \"Test User\"");
        Assert.True(nameResult.IsSuccess);

        // Create initial commit
        File.WriteAllText(Path.Combine(_repoPath, "initial.txt"), "initial");
        await _gitRunner.ExecuteAsync("add initial.txt");
        await _gitRunner.ExecuteAsync("commit -m \"Initial commit\"");
    }

    /// <summary>
    /// Creates a test file in the repository.
    /// </summary>
    private string CreateTestFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(_repoPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        File.WriteAllText(fullPath, content);
        return relativePath;
    }

    /// <summary>
    /// Creates the task structure with plan.json containing file scopes.
    /// </summary>
    private void CreateTaskStructure(string taskId, string[] fileScopes)
    {
        var taskDir = Path.Combine(_repoPath, ".aos", "spec", "tasks", taskId);
        Directory.CreateDirectory(taskDir);

        var plan = new
        {
            fileScopes,
            taskId
        };

        File.WriteAllText(
            Path.Combine(taskDir, "plan.json"),
            JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Reads and parses the task evidence latest.json file.
    /// </summary>
    private async Task<TaskEvidenceData?> ReadTaskEvidenceAsync(string aosRootPath, string taskId)
    {
        var path = AosPathRouter.GetTaskEvidenceLatestPath(aosRootPath, taskId);
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<TaskEvidenceData>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Reads the git-commit.json artifact for a run.
    /// </summary>
    private async Task<GitCommitArtifact?> ReadCommitArtifactAsync(string aosRootPath, string runId)
    {
        var path = Path.Combine(AosPathRouter.GetRunArtifactsRootPath(aosRootPath, runId), "git-commit.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<GitCommitArtifact>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Reads the git-diffstat.json artifact for a run.
    /// </summary>
    private async Task<GitDiffStatArtifact?> ReadDiffStatArtifactAsync(string aosRootPath, string runId)
    {
        var path = Path.Combine(AosPathRouter.GetRunArtifactsRootPath(aosRootPath, runId), "git-diffstat.json");
        if (!File.Exists(path))
        {
            return null;
        }

        var content = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<GitDiffStatArtifact>(content, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    [Fact]
    public async Task CommitAsync_SuccessfulCommitWithScopedFiles_UpdatesEvidence()
    {
        // Arrange
        await InitializeGitRepoAsync();
        var workspace = CreateTestWorkspace();
        // Use valid 32-char hex run ID
        var runId = Guid.NewGuid().ToString("N");
        var taskId = "TSK-000001";
        var fileScopes = new[] { "src/**/*.cs" };

        CreateTaskStructure(taskId, fileScopes);

        // Create files within scope
        CreateTestFile("src/File1.cs", "namespace Test; public class File1 {}");
        CreateTestFile("src/Utils/Helper.cs", "namespace Test.Utils; public class Helper {}");

        var committer = new AtomicGitCommitterAlias(workspace, _logger);
        var request = new CommitRequest
        {
            TaskId = taskId,
            FileScopes = fileScopes,
            ChangedFiles = new[] { "src/File1.cs", "src/Utils/Helper.cs" },
            Summary = "Add new feature files"
        };

        // Create run artifacts directory for evidence
        Directory.CreateDirectory(AosPathRouter.GetRunArtifactsRootPath(workspace.AosRootPath, runId));

        // Act
        var result = await committer.CommitAsync(request);

        // Write evidence files as the committer would
        if (result.IsSuccess)
        {
            CommitEvidenceWriter.WriteDiffStat(workspace.AosRootPath, runId, result.DiffStat!);
            if (result.CommitHash != null)
            {
                CommitEvidenceWriter.WriteCommit(
                    workspace.AosRootPath,
                    runId,
                    result.CommitHash,
                    DateTimeOffset.UtcNow,
                    $"{taskId}: {request.Summary}");
            }
            else
            {
                CommitEvidenceWriter.WriteNoCommit(workspace.AosRootPath, runId, "No files to commit");
            }
        }

        // Update task evidence
        TaskEvidenceUpdater.UpdateWithCommit(
            workspace.AosRootPath,
            taskId,
            runId,
            result.CommitHash,
            result.DiffStat!);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CommitHash);
        Assert.NotNull(result.DiffStat);
        Assert.Equal(2, result.DiffStat.FilesChanged);
        Assert.Equal(2, result.FilesStaged.Count);
        Assert.Contains("src/File1.cs", result.FilesStaged);
        Assert.Contains("src/Utils/Helper.cs", result.FilesStaged);

        // Verify evidence artifacts
        var diffStatArtifact = await ReadDiffStatArtifactAsync(workspace.AosRootPath, runId);
        Assert.NotNull(diffStatArtifact);
        Assert.Equal(2, diffStatArtifact.FilesChanged);
        Assert.True(diffStatArtifact.Insertions > 0);

        var commitArtifact = await ReadCommitArtifactAsync(workspace.AosRootPath, runId);
        Assert.NotNull(commitArtifact);
        Assert.NotNull(commitArtifact.CommitHash);
        Assert.Contains(taskId, commitArtifact.Message);

        // Verify task evidence pointer
        var taskEvidence = await ReadTaskEvidenceAsync(workspace.AosRootPath, taskId);
        Assert.NotNull(taskEvidence);
        Assert.Equal(taskId, taskEvidence.TaskId);
        Assert.Equal(runId, taskEvidence.RunId);
        Assert.NotNull(taskEvidence.GitCommit);
        Assert.Equal(result.CommitHash, taskEvidence.GitCommit);
        Assert.NotNull(taskEvidence.Diffstat);
        Assert.Equal(2, taskEvidence.Diffstat.FilesChanged);

        _output.WriteLine($"Commit hash: {result.CommitHash}");
        _output.WriteLine($"Files staged: {string.Join(", ", result.FilesStaged)}");
    }

    [Fact]
    public async Task CommitAsync_ForbiddenFiles_NotStagedEvenIfModified()
    {
        // Arrange
        await InitializeGitRepoAsync();
        var workspace = CreateTestWorkspace();
        var runId = Guid.NewGuid().ToString("N");
        var taskId = "TSK-000002";
        var fileScopes = new[] { "src/**/*.cs" };

        CreateTaskStructure(taskId, fileScopes);

        // Create files - one in scope, one forbidden
        CreateTestFile("src/InScope.cs", "namespace Test; public class InScope {}");
        CreateTestFile("tests/Forbidden.cs", "namespace Tests; public class Forbidden {}");
        CreateTestFile("README.md", "# Project");

        var committer = new AtomicGitCommitterAlias(workspace, _logger);
        var request = new CommitRequest
        {
            TaskId = taskId,
            FileScopes = fileScopes,
            ChangedFiles = new[] { "src/InScope.cs", "tests/Forbidden.cs", "README.md" },
            Summary = "Try to commit out of scope files"
        };

        Directory.CreateDirectory(AosPathRouter.GetRunArtifactsRootPath(workspace.AosRootPath, runId));

        // Act
        var result = await committer.CommitAsync(request);

        // Write evidence files
        if (result.IsSuccess)
        {
            CommitEvidenceWriter.WriteDiffStat(workspace.AosRootPath, runId, result.DiffStat!);
            if (result.CommitHash != null)
            {
                CommitEvidenceWriter.WriteCommit(
                    workspace.AosRootPath,
                    runId,
                    result.CommitHash,
                    DateTimeOffset.UtcNow,
                    $"{taskId}: {request.Summary}");
            }
            else
            {
                CommitEvidenceWriter.WriteNoCommit(workspace.AosRootPath, runId, "No files to commit");
            }
        }

        TaskEvidenceUpdater.UpdateWithCommit(
            workspace.AosRootPath,
            taskId,
            runId,
            result.CommitHash,
            result.DiffStat!);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CommitHash);
        Assert.Single(result.FilesStaged);
        Assert.Contains("src/InScope.cs", result.FilesStaged);
        Assert.DoesNotContain("tests/Forbidden.cs", result.FilesStaged);
        Assert.DoesNotContain("README.md", result.FilesStaged);

        // Verify git status shows forbidden files are NOT staged
        var statusResult = await _gitRunner.ExecuteAsync("status --porcelain");
        var statusOutput = statusResult.Stdout.Replace('\\', '/');
        
        // Git may report untracked directory as "tests/" instead of individual files
        if (!statusOutput.Contains("tests/Forbidden.cs"))
        {
            Assert.Contains("tests/", statusOutput);
        }
        else
        {
            Assert.Contains("tests/Forbidden.cs", statusOutput);
        }
        
        Assert.Contains("README.md", statusOutput); // Still untracked/modified
        Assert.DoesNotContain("src/InScope.cs", statusOutput); // Staged and committed

        // Verify commit only contains in-scope file
        var logResult = await _gitRunner.ExecuteAsync("log -1 --name-only");
        var logOutput = logResult.Stdout.Replace('\\', '/');
        Assert.Contains("src/InScope.cs", logOutput);
        Assert.DoesNotContain("tests/Forbidden.cs", logOutput);
        Assert.DoesNotContain("README.md", logOutput);

        _output.WriteLine($"Files staged: {string.Join(", ", result.FilesStaged)}");
        _output.WriteLine($"Git status:\n{statusResult.Stdout}");
    }

    [Fact]
    public async Task CommitAsync_EmptyIntersection_ProducesNullCommitInEvidence()
    {
        // Arrange
        await InitializeGitRepoAsync();
        var workspace = CreateTestWorkspace();
        var runId = Guid.NewGuid().ToString("N");
        var taskId = "TSK-000003";
        var fileScopes = new[] { "src/**/*.cs" };

        CreateTaskStructure(taskId, fileScopes);

        // Create files OUTSIDE of scope (in tests folder only)
        CreateTestFile("tests/Test1.cs", "namespace Tests; public class Test1 {}");
        CreateTestFile("tests/Test2.cs", "namespace Tests; public class Test2 {}");

        var committer = new AtomicGitCommitterAlias(workspace, _logger);
        var request = new CommitRequest
        {
            TaskId = taskId,
            FileScopes = fileScopes,
            ChangedFiles = new[] { "tests/Test1.cs", "tests/Test2.cs" },
            Summary = "Only out of scope changes"
        };

        Directory.CreateDirectory(AosPathRouter.GetRunArtifactsRootPath(workspace.AosRootPath, runId));

        // Act
        var result = await committer.CommitAsync(request);

        // Write evidence files
        if (result.IsSuccess)
        {
            CommitEvidenceWriter.WriteDiffStat(workspace.AosRootPath, runId, result.DiffStat!);
            if (result.CommitHash != null)
            {
                CommitEvidenceWriter.WriteCommit(
                    workspace.AosRootPath,
                    runId,
                    result.CommitHash,
                    DateTimeOffset.UtcNow,
                    $"{taskId}: {request.Summary}");
            }
            else
            {
                CommitEvidenceWriter.WriteNoCommit(workspace.AosRootPath, runId, "No files to commit - empty intersection");
            }
        }

        TaskEvidenceUpdater.UpdateWithoutCommit(
            workspace.AosRootPath,
            taskId,
            runId,
            result.DiffStat!);

        // Assert
        Assert.True(result.IsSuccess); // Not a failure - just nothing to do
        Assert.Null(result.CommitHash);
        Assert.NotNull(result.DiffStat);
        Assert.Equal(0, result.DiffStat.FilesChanged);
        Assert.Equal(0, result.DiffStat.Insertions);
        Assert.Equal(0, result.DiffStat.Deletions);
        Assert.Empty(result.FilesStaged);

        // Verify no new commit was created
        var logResult = await _gitRunner.ExecuteAsync("log --oneline");
        var commitCount = logResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        Assert.Equal(1, commitCount); // Only initial commit

        // Verify task evidence has null commit
        var taskEvidence = await ReadTaskEvidenceAsync(workspace.AosRootPath, taskId);
        Assert.NotNull(taskEvidence);
        Assert.Null(taskEvidence.GitCommit);
        Assert.Equal(0, taskEvidence.Diffstat.FilesChanged);

        _output.WriteLine("Empty intersection correctly produced null commit");
        _output.WriteLine($"Diffstat: {result.DiffStat.FilesChanged} files, {result.DiffStat.Insertions} insertions");
    }

    [Fact]
    public async Task CommitAsync_CommitMessageFormat_IsTSKBased()
    {
        // Arrange
        await InitializeGitRepoAsync();
        var workspace = CreateTestWorkspace();
        var runId = Guid.NewGuid().ToString("N");
        var taskId = "TSK-000004";
        var fileScopes = new[] { "*.txt" };

        CreateTaskStructure(taskId, fileScopes);

        CreateTestFile("file1.txt", "content1");
        CreateTestFile("file2.txt", "content2");

        var committer = new AtomicGitCommitterAlias(workspace, _logger);
        var summary = "Add text files for testing";
        var request = new CommitRequest
        {
            TaskId = taskId,
            FileScopes = fileScopes,
            ChangedFiles = new[] { "file1.txt", "file2.txt" },
            Summary = summary
        };

        Directory.CreateDirectory(AosPathRouter.GetRunArtifactsRootPath(workspace.AosRootPath, runId));

        // Act
        var result = await committer.CommitAsync(request);

        // Write evidence
        if (result.IsSuccess && result.CommitHash != null)
        {
            CommitEvidenceWriter.WriteDiffStat(workspace.AosRootPath, runId, result.DiffStat!);
            CommitEvidenceWriter.WriteCommit(
                workspace.AosRootPath,
                runId,
                result.CommitHash,
                DateTimeOffset.UtcNow,
                $"{taskId}: {summary}");

            TaskEvidenceUpdater.UpdateWithCommit(
                workspace.AosRootPath,
                taskId,
                runId,
                result.CommitHash,
                result.DiffStat!);
        }

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.CommitHash);

        // Verify commit message format in git log
        var logResult = await _gitRunner.ExecuteAsync("log -1 --pretty=%B");
        var commitMessage = logResult.Stdout.Trim();

        // Format should be: TSK-XXXXXX: <summary>
        Assert.StartsWith(taskId, commitMessage);
        Assert.Contains(": ", commitMessage);
        Assert.Contains(summary, commitMessage);

        // Verify the format is exactly TSK-XXXXXX: <summary>
        var expectedPrefix = $"{taskId}: ";
        Assert.StartsWith(expectedPrefix, commitMessage);

        _output.WriteLine($"Commit message: {commitMessage}");
    }

    [Fact]
    public async Task CommitAsync_Rerun_ProducesNewCommitHashAndUpdatedEvidence()
    {
        // Arrange
        await InitializeGitRepoAsync();
        var workspace = CreateTestWorkspace();
        var taskId = "TSK-000005";
        var fileScopes = new[] { "src/**/*.cs" };

        CreateTaskStructure(taskId, fileScopes);

        // First run
        var runId1 = Guid.NewGuid().ToString("N");
        CreateTestFile("src/First.cs", "namespace Test; public class First {}");

        var committer = new AtomicGitCommitterAlias(workspace, _logger);
        var request1 = new CommitRequest
        {
            TaskId = taskId,
            FileScopes = fileScopes,
            ChangedFiles = new[] { "src/First.cs" },
            Summary = "First commit"
        };

        Directory.CreateDirectory(AosPathRouter.GetRunArtifactsRootPath(workspace.AosRootPath, runId1));

        var result1 = await committer.CommitAsync(request1);

        if (result1.IsSuccess && result1.CommitHash != null)
        {
            CommitEvidenceWriter.WriteDiffStat(workspace.AosRootPath, runId1, result1.DiffStat!);
            CommitEvidenceWriter.WriteCommit(
                workspace.AosRootPath,
                runId1,
                result1.CommitHash,
                DateTimeOffset.UtcNow,
                $"{taskId}: {request1.Summary}");
            TaskEvidenceUpdater.UpdateWithCommit(
                workspace.AosRootPath,
                taskId,
                runId1,
                result1.CommitHash,
                result1.DiffStat!);
        }

        Assert.True(result1.IsSuccess);
        Assert.NotNull(result1.CommitHash);

        // Create another file for second run
        var runId2 = Guid.NewGuid().ToString("N");
        CreateTestFile("src/Second.cs", "namespace Test; public class Second {}");

        // Reset file tracking - only second file is changed now
        var request2 = new CommitRequest
        {
            TaskId = taskId,
            FileScopes = fileScopes,
            ChangedFiles = new[] { "src/Second.cs" },
            Summary = "Second commit"
        };

        Directory.CreateDirectory(AosPathRouter.GetRunArtifactsRootPath(workspace.AosRootPath, runId2));

        // Act - Second run
        var result2 = await committer.CommitAsync(request2);

        if (result2.IsSuccess && result2.CommitHash != null)
        {
            CommitEvidenceWriter.WriteDiffStat(workspace.AosRootPath, runId2, result2.DiffStat!);
            CommitEvidenceWriter.WriteCommit(
                workspace.AosRootPath,
                runId2,
                result2.CommitHash,
                DateTimeOffset.UtcNow,
                $"{taskId}: {request2.Summary}");
            TaskEvidenceUpdater.UpdateWithCommit(
                workspace.AosRootPath,
                taskId,
                runId2,
                result2.CommitHash,
                result2.DiffStat!);
        }

        // Assert
        Assert.True(result2.IsSuccess);
        Assert.NotNull(result2.CommitHash);

        // Verify different commit hashes
        Assert.NotEqual(result1.CommitHash, result2.CommitHash);

        // Verify task evidence is updated to latest run
        var taskEvidence = await ReadTaskEvidenceAsync(workspace.AosRootPath, taskId);
        Assert.NotNull(taskEvidence);
        Assert.Equal(runId2, taskEvidence.RunId); // Updated to second run
        Assert.Equal(result2.CommitHash, taskEvidence.GitCommit); // Updated to second commit

        // Verify both evidence artifacts exist
        var commitArtifact1 = await ReadCommitArtifactAsync(workspace.AosRootPath, runId1);
        var commitArtifact2 = await ReadCommitArtifactAsync(workspace.AosRootPath, runId2);

        Assert.NotNull(commitArtifact1);
        Assert.NotNull(commitArtifact2);
        Assert.Equal(result1.CommitHash, commitArtifact1.CommitHash);
        Assert.Equal(result2.CommitHash, commitArtifact2.CommitHash);

        // Verify git log has both commits
        var logResult = await _gitRunner.ExecuteAsync("log --oneline");
        Assert.Contains(result1.CommitHash[..7], logResult.Stdout);
        Assert.Contains(result2.CommitHash[..7], logResult.Stdout);

        _output.WriteLine($"First commit: {result1.CommitHash}");
        _output.WriteLine($"Second commit: {result2.CommitHash}");
        _output.WriteLine($"Git log:\n{logResult.Stdout}");
    }

    // Test helper classes and records

    private class TestWorkspace : IWorkspace
    {
        public TestWorkspace(string repositoryRootPath)
        {
            RepositoryRootPath = repositoryRootPath;
            AosRootPath = Path.Combine(repositoryRootPath, ".aos");
        }

        public string RepositoryRootPath { get; }
        public string AosRootPath { get; }

        public string GetContractPathForArtifactId(string artifactId) => throw new NotImplementedException();
        public string GetAbsolutePathForContractPath(string contractPath) => throw new NotImplementedException();
        public string GetAbsolutePathForArtifactId(string artifactId) => throw new NotImplementedException();
        public JsonElement ReadArtifact(string subpath, string filename) => throw new NotImplementedException();
    }

    private class TaskEvidenceData
    {
        public int SchemaVersion { get; set; }
        public string TaskId { get; set; } = "";
        public string RunId { get; set; } = "";
        public string? GitCommit { get; set; }
        public DiffStatData Diffstat { get; set; } = new();
    }

    private class DiffStatData
    {
        public int FilesChanged { get; set; }
        public int Insertions { get; set; }
        public int Deletions { get; set; }
    }

    private class GitCommitArtifact
    {
        public int SchemaVersion { get; set; }
        public string? CommitHash { get; set; }
        public string? TimestampUtc { get; set; }
        public string Message { get; set; } = "";
    }

    private class GitDiffStatArtifact
    {
        public int SchemaVersion { get; set; }
        public int FilesChanged { get; set; }
        public int Insertions { get; set; }
        public int Deletions { get; set; }
    }
}
