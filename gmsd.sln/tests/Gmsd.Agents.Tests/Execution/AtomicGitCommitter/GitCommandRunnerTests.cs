using System.Text;
using Gmsd.Agents.Execution.Execution.AtomicGitCommitter;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.AtomicGitCommitter;

public class GitCommandRunnerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly GitCommandRunner _runner;

    public GitCommandRunnerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"git-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _runner = new GitCommandRunner(_tempDir);
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

    #region Constructor Tests

    [Fact]
    public void Constructor_NullWorkingDirectory_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GitCommandRunner(null!));
    }

    [Fact]
    public void Constructor_NullGitExecutable_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new GitCommandRunner("/some/path", null!));
    }

    #endregion

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_InvalidCommand_ReturnsErrorExitCode()
    {
        var result = await _runner.ExecuteAsync("not-a-real-command");

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotNull(result.Stderr);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsSuccess()
    {
        // Initialize repo first
        await _runner.ExecuteAsync("init");

        var result = await _runner.ExecuteAsync("status");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
        Assert.NotNull(result.Stdout);
    }

    [Fact]
    public async Task ExecuteAsync_CapturesStdout()
    {
        await _runner.ExecuteAsync("init");
        await _runner.ExecuteAsync("config user.email \"test@test.com\"");
        await _runner.ExecuteAsync("config user.name \"Test\"");

        // Create and commit a file
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "content");
        await _runner.ExecuteAsync("add test.txt");
        await _runner.ExecuteAsync("commit -m \"initial\"");

        var result = await _runner.ExecuteAsync("log --oneline");

        Assert.Contains("initial", result.Stdout);
    }

    #endregion

    #region StageFilesAsync Tests

    [Fact]
    public async Task StageFilesAsync_EmptyList_ReturnsSuccessWithMessage()
    {
        var result = await _runner.StageFilesAsync(new List<string>());

        Assert.True(result.IsSuccess);
        Assert.Contains("No files", result.Stdout);
    }

    [Fact]
    public async Task StageFilesAsync_NullFiles_ThrowsArgumentNullException()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _runner.StageFilesAsync(null!));
    }

    [Fact]
    public async Task StageFilesAsync_SingleFile_StagesSuccessfully()
    {
        await _runner.ExecuteAsync("init");
        var filePath = Path.Combine(_tempDir, "test.txt");
        File.WriteAllText(filePath, "content");

        var result = await _runner.StageFilesAsync(new[] { "test.txt" });

        Assert.True(result.IsSuccess);

        // Verify file is staged
        var statusResult = await _runner.ExecuteAsync("diff --cached --name-only");
        Assert.Contains("test.txt", statusResult.Stdout);
    }

    [Fact]
    public async Task StageFilesAsync_MultipleFiles_StagesAll()
    {
        await _runner.ExecuteAsync("init");
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "a");
        File.WriteAllText(Path.Combine(_tempDir, "b.txt"), "b");

        var result = await _runner.StageFilesAsync(new[] { "a.txt", "b.txt" });

        Assert.True(result.IsSuccess);

        var statusResult = await _runner.ExecuteAsync("diff --cached --name-only");
        Assert.Contains("a.txt", statusResult.Stdout);
        Assert.Contains("b.txt", statusResult.Stdout);
    }

    [Fact]
    public async Task StageFilesAsync_FilesWithSpaces_StagesSuccessfully()
    {
        await _runner.ExecuteAsync("init");
        File.WriteAllText(Path.Combine(_tempDir, "file with spaces.txt"), "content");

        var result = await _runner.StageFilesAsync(new[] { "file with spaces.txt" });

        Assert.True(result.IsSuccess);

        var statusResult = await _runner.ExecuteAsync("diff --cached --name-only");
        Assert.Contains("file with spaces.txt", statusResult.Stdout);
    }

    [Fact]
    public async Task StageFilesAsync_NonExistentFile_ReturnsError()
    {
        await _runner.ExecuteAsync("init");

        var result = await _runner.StageFilesAsync(new[] { "nonexistent.txt" });

        Assert.False(result.IsSuccess);
    }

    #endregion

    #region CommitAsync Tests

    [Fact]
    public async Task CommitAsync_EmptyMessage_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _runner.CommitAsync(""));
    }

    [Fact]
    public async Task CommitAsync_WhitespaceMessage_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _runner.CommitAsync("   "));
    }

    [Fact]
    public async Task CommitAsync_NullMessage_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _runner.CommitAsync(null!));
    }

    [Fact]
    public async Task CommitAsync_ValidMessage_CreatesCommit()
    {
        await _runner.ExecuteAsync("init");
        await _runner.ExecuteAsync("config user.email \"test@test.com\"");
        await _runner.ExecuteAsync("config user.name \"Test\"");

        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "content");
        await _runner.ExecuteAsync("add test.txt");

        var result = await _runner.CommitAsync("TSK-0001: Test commit");

        Assert.True(result.IsSuccess);
        Assert.Contains("TSK-0001", result.Stdout);
    }

    [Fact]
    public async Task CommitAsync_MessageWithQuotes_EscapesProperly()
    {
        await _runner.ExecuteAsync("init");
        await _runner.ExecuteAsync("config user.email \"test@test.com\"");
        await _runner.ExecuteAsync("config user.name \"Test\"");

        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "content");
        await _runner.ExecuteAsync("add test.txt");

        var result = await _runner.CommitAsync("TSK-0001: Fix \"quoted\" text");

        Assert.True(result.IsSuccess);

        var logResult = await _runner.ExecuteAsync("log -1 --pretty=%B");
        Assert.Contains("\"quoted\"", logResult.Stdout);
    }

    [Fact]
    public async Task CommitAsync_NoStagedChanges_ReturnsError()
    {
        await _runner.ExecuteAsync("init");
        await _runner.ExecuteAsync("config user.email \"test@test.com\"");
        await _runner.ExecuteAsync("config user.name \"Test\"");

        var result = await _runner.CommitAsync("TSK-0001: No changes");

        Assert.False(result.IsSuccess);
    }

    #endregion

    #region GetHeadCommitHashAsync Tests

    [Fact]
    public async Task GetHeadCommitHashAsync_NoCommits_ReturnsNull()
    {
        await _runner.ExecuteAsync("init");

        var hash = await _runner.GetHeadCommitHashAsync();

        Assert.Null(hash);
    }

    [Fact]
    public async Task GetHeadCommitHashAsync_WithCommit_ReturnsShortHash()
    {
        await _runner.ExecuteAsync("init");
        await _runner.ExecuteAsync("config user.email \"test@test.com\"");
        await _runner.ExecuteAsync("config user.name \"Test\"");

        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "content");
        await _runner.ExecuteAsync("add test.txt");
        await _runner.ExecuteAsync("commit -m \"initial\"");

        var hash = await _runner.GetHeadCommitHashAsync();

        Assert.NotNull(hash);
        Assert.InRange(hash.Length, 7, 40); // Short hash length varies
        Assert.All(hash, c => Assert.True(char.IsLetterOrDigit(c)));
    }

    #endregion

    #region GetStagedDiffStatAsync Tests

    [Fact]
    public async Task GetStagedDiffStatAsync_NoStagedChanges_ReturnsEmpty()
    {
        await _runner.ExecuteAsync("init");

        var stat = await _runner.GetStagedDiffStatAsync();

        Assert.NotNull(stat);
        Assert.Empty(stat);
    }

    [Fact]
    public async Task GetStagedDiffStatAsync_WithStagedChanges_ReturnsStats()
    {
        await _runner.ExecuteAsync("init");
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "line1\nline2\nline3");
        await _runner.ExecuteAsync("add test.txt");

        var stat = await _runner.GetStagedDiffStatAsync();

        Assert.NotNull(stat);
        Assert.Contains("test.txt", stat);
    }

    #endregion

    #region IsGitRepositoryAsync Tests

    [Fact]
    public async Task IsGitRepositoryAsync_UninitializedDirectory_ReturnsFalse()
    {
        var isRepo = await _runner.IsGitRepositoryAsync();

        Assert.False(isRepo);
    }

    [Fact]
    public async Task IsGitRepositoryAsync_InitializedRepo_ReturnsTrue()
    {
        await _runner.ExecuteAsync("init");

        var isRepo = await _runner.IsGitRepositoryAsync();

        Assert.True(isRepo);
    }

    [Fact]
    public async Task IsGitRepositoryAsync_NestedDirectory_ReturnsTrue()
    {
        await _runner.ExecuteAsync("init");
        var nestedDir = Path.Combine(_tempDir, "nested", "deep");
        Directory.CreateDirectory(nestedDir);
        var nestedRunner = new GitCommandRunner(nestedDir);

        var isRepo = await nestedRunner.IsGitRepositoryAsync();

        Assert.True(isRepo);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ExecuteAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        await _runner.ExecuteAsync("init");

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _runner.ExecuteAsync("status", cts.Token));
    }

    #endregion
}
