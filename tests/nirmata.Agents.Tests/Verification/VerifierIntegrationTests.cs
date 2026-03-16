using FluentAssertions;
using nirmata.Agents.Execution.Verification.Issues;
using nirmata.Agents.Execution.Verification.UatVerifier;
using nirmata.Aos.Public;
using nirmata.Aos.Public.Services;
using System.Text.Json;
using Xunit;

namespace nirmata.Agents.Tests.Verification;

public class VerifierIntegrationTests : IDisposable
{
    private readonly string _tempWorkspacePath;
    private readonly string _aosRootPath;
    private readonly UatCheckRunner _checkRunner;
    private readonly IssueWriter _issueWriter;
    private readonly UatResultWriter _resultWriter;
    private readonly UatVerifier _verifier;

    public VerifierIntegrationTests()
    {
        // Create temp workspace
        _tempWorkspacePath = Path.Combine(Path.GetTempPath(), $"nirmata-test-{Guid.NewGuid():N}");
        _aosRootPath = Path.Combine(_tempWorkspacePath, ".aos");
        Directory.CreateDirectory(_aosRootPath);
        Directory.CreateDirectory(Path.Combine(_aosRootPath, "spec", "issues"));
        Directory.CreateDirectory(Path.Combine(_aosRootPath, "evidence", "runs"));

        // Create a simple workspace implementation for testing
        var workspace = new TestWorkspace(_tempWorkspacePath, _aosRootPath);
        var jsonSerializer = new TestDeterministicJsonSerializer();

        _checkRunner = new UatCheckRunner();
        _issueWriter = new IssueWriter(workspace, jsonSerializer);
        _resultWriter = new UatResultWriter(workspace, jsonSerializer);
        _verifier = new UatVerifier(_checkRunner, _issueWriter, _resultWriter, workspace);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempWorkspacePath))
            {
                Directory.Delete(_tempWorkspacePath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public async Task EndToEnd_FileExistsCheck_PassesWhenFileExists()
    {
        // Arrange: Create the file
        var testFilePath = Path.Combine(_tempWorkspacePath, "src", "test.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(testFilePath)!);
        await File.WriteAllTextAsync(testFilePath, "test content");

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-001",
            AcceptanceCriteria = new[]
            {
                new AcceptanceCriterion
                {
                    Id = "criterion-001",
                    Description = "Test file should exist",
                    CheckType = UatCheckTypes.FileExists,
                    TargetPath = "src/test.txt",
                    IsRequired = true
                }
            }.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        // Act
        var result = await _verifier.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.Checks.Should().HaveCount(1);
        result.Checks[0].Passed.Should().BeTrue();

        // Verify artifact was written
        var artifactPath = Path.Combine(_aosRootPath, "evidence", "runs", "RUN-001", "artifacts", "uat-results.json");
        File.Exists(artifactPath).Should().BeTrue();

        var json = await File.ReadAllTextAsync(artifactPath);
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var artifact = JsonSerializer.Deserialize<UatResult>(json, options);
        artifact.Should().NotBeNull();
        artifact!.Status.Should().Be("passed");
        artifact.TaskId.Should().Be("TASK-001");
        artifact.RunId.Should().Be("RUN-001");
    }

    [Fact]
    public async Task EndToEnd_FileExistsCheck_FailsWhenFileMissing()
    {
        // Arrange: Don't create the file
        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-002",
            AcceptanceCriteria = new[]
            {
                new AcceptanceCriterion
                {
                    Id = "criterion-001",
                    Description = "Test file should exist",
                    CheckType = UatCheckTypes.FileExists,
                    TargetPath = "src/missing.txt",
                    IsRequired = true
                }
            }.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        // Act
        var result = await _verifier.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.IssuesCreated.Should().HaveCount(1);

        // Verify issue was created
        var issuePath = result.IssuesCreated[0].IssuePath;
        File.Exists(issuePath).Should().BeTrue();

        var issueJson = await File.ReadAllTextAsync(issuePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var issue = JsonSerializer.Deserialize<Issue>(issueJson, options);
        issue.Should().NotBeNull();
        issue!.Scope.Should().Be("src/missing.txt");
        issue.Severity.Should().Be("high");
    }

    [Fact]
    public async Task EndToEnd_ContentContainsCheck_PassesWhenContentFound()
    {
        // Arrange: Create file with expected content
        var testFilePath = Path.Combine(_tempWorkspacePath, "src", "code.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(testFilePath)!);
        await File.WriteAllTextAsync(testFilePath, "public class TestClass { }");

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-003",
            AcceptanceCriteria = new[]
            {
                new AcceptanceCriterion
                {
                    Id = "criterion-001",
                    Description = "File should contain class definition",
                    CheckType = UatCheckTypes.ContentContains,
                    TargetPath = "src/code.cs",
                    ExpectedContent = "class TestClass",
                    IsRequired = true
                }
            }.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        // Act
        var result = await _verifier.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.Checks[0].Passed.Should().BeTrue();
        result.Checks[0].Expected.Should().Be("class TestClass");
        result.Checks[0].Actual.Should().Be("content found");
    }

    [Fact]
    public async Task EndToEnd_ContentContainsCheck_FailsWhenContentNotFound()
    {
        // Arrange: Create file without expected content
        var testFilePath = Path.Combine(_tempWorkspacePath, "src", "code.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(testFilePath)!);
        await File.WriteAllTextAsync(testFilePath, "public class OtherClass { }");

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-004",
            AcceptanceCriteria = new[]
            {
                new AcceptanceCriterion
                {
                    Id = "criterion-001",
                    Description = "File should contain specific method",
                    CheckType = UatCheckTypes.ContentContains,
                    TargetPath = "src/code.cs",
                    ExpectedContent = "public void TestMethod",
                    IsRequired = true
                }
            }.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        // Act
        var result = await _verifier.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Checks[0].Passed.Should().BeFalse();
        result.Checks[0].Expected.Should().Be("public void TestMethod");
        result.Checks[0].Actual.Should().Be("content not found");
    }

    [Fact]
    public async Task EndToEnd_MultipleChecksWithPartialFailure_CreatesIssuesForFailedChecks()
    {
        // Arrange: Create only one of two required files
        var file1Path = Path.Combine(_tempWorkspacePath, "src", "file1.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(file1Path)!);
        await File.WriteAllTextAsync(file1Path, "content");

        var request = new UatVerificationRequest
        {
            TaskId = "TASK-001",
            RunId = "RUN-005",
            AcceptanceCriteria = new[]
            {
                new AcceptanceCriterion
                {
                    Id = "criterion-001",
                    Description = "File 1 should exist",
                    CheckType = UatCheckTypes.FileExists,
                    TargetPath = "src/file1.txt",
                    IsRequired = true
                },
                new AcceptanceCriterion
                {
                    Id = "criterion-002",
                    Description = "File 2 should exist",
                    CheckType = UatCheckTypes.FileExists,
                    TargetPath = "src/file2.txt",
                    IsRequired = true
                }
            }.AsReadOnly(),
            FileScopes = Array.Empty<FileScope>().AsReadOnly()
        };

        // Act
        var result = await _verifier.VerifyAsync(request);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Checks.Should().HaveCount(2);
        result.Checks.Count(c => c.Passed).Should().Be(1);
        result.Checks.Count(c => !c.Passed).Should().Be(1);
        result.IssuesCreated.Should().HaveCount(1); // Only for the failed required check
    }

    // Test implementations
    private class TestWorkspace : IWorkspace
    {
        public TestWorkspace(string repoRoot, string aosRoot)
        {
            RepositoryRootPath = repoRoot;
            AosRootPath = aosRoot;
        }

        public string RepositoryRootPath { get; }
        public string AosRootPath { get; }

        public string GetAbsolutePathForArtifactId(string artifactId) => throw new NotImplementedException();
        public string GetContractPathForArtifactId(string artifactId) => throw new NotImplementedException();
        public string GetAbsolutePathForContractPath(string contractPath) => throw new NotImplementedException();
        public JsonElement ReadArtifact(string subpath, string filename) => throw new NotImplementedException();
    }

    private class TestDeterministicJsonSerializer : IDeterministicJsonSerializer
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        public byte[] Serialize<T>(T value, JsonSerializerOptions serializerOptions, bool writeIndented = true) 
            => JsonSerializer.SerializeToUtf8Bytes(value, serializerOptions ?? Options);
        
        public string SerializeToString<T>(T value, JsonSerializerOptions serializerOptions, bool writeIndented = true) 
            => JsonSerializer.Serialize(value, serializerOptions ?? Options);
        
        public T Deserialize<T>(byte[] jsonBytes, JsonSerializerOptions serializerOptions) 
            => JsonSerializer.Deserialize<T>(jsonBytes, serializerOptions ?? Options)!;
        
        public T Deserialize<T>(string json, JsonSerializerOptions serializerOptions) 
            => JsonSerializer.Deserialize<T>(json, serializerOptions ?? Options)!;
        
        public void WriteAtomic<T>(string path, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
        {
            var json = SerializeToString(value, serializerOptions, writeIndented);
            File.WriteAllText(path, json);
        }
        
        public void WriteIfMissing<T>(string path, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
        {
            if (!File.Exists(path))
            {
                WriteAtomic(path, value, serializerOptions, writeIndented);
            }
        }
        
        public T ReadFile<T>(string path, JsonSerializerOptions serializerOptions)
        {
            var json = File.ReadAllText(path);
            return Deserialize<T>(json, serializerOptions);
        }
    }
}
