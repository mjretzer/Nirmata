using FluentAssertions;
using Gmsd.Agents.Execution.Verification.UatVerifier;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Gmsd.Agents.Tests.Verification;

public class UatArtifactSchemaTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [Fact]
    public void UatResult_Serializes_WithCorrectSchemaVersion()
    {
        var result = new UatResult
        {
            SchemaVersion = "gmsd:aos:schema:uat-result:v1",
            RunId = "RUN-001",
            TaskId = "TASK-001",
            Status = "passed",
            Timestamp = DateTimeOffset.Parse("2026-02-06T12:00:00Z"),
            Checks = new[]
            {
                new UatCheckRecord
                {
                    CriterionId = "criterion-001",
                    Passed = true,
                    Message = "Check passed",
                    CheckType = "file-exists",
                    TargetPath = "src/test.txt",
                    Expected = "file exists",
                    Actual = "file exists"
                }
            }.AsReadOnly()
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);

        // Verify required fields are present
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"runId\"");
        json.Should().Contain("\"taskId\"");
        json.Should().Contain("\"status\"");
        json.Should().Contain("\"timestamp\"");
        json.Should().Contain("\"checks\"");

        // Verify schema version value
        json.Should().Contain("gmsd:aos:schema:uat-result:v1");
    }

    [Fact]
    public void UatResult_Deserializes_Correctly()
    {
        var json = @"{
            ""schemaVersion"": ""gmsd:aos:schema:uat-result:v1"",
            ""runId"": ""RUN-001"",
            ""taskId"": ""TASK-001"",
            ""status"": ""passed"",
            ""timestamp"": ""2026-02-06T12:00:00+00:00"",
            ""checks"": [
                {
                    ""criterionId"": ""criterion-001"",
                    ""passed"": true,
                    ""message"": ""File exists"",
                    ""checkType"": ""file-exists"",
                    ""targetPath"": ""src/test.txt""
                }
            ]
        }";

        var result = JsonSerializer.Deserialize<UatResult>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.SchemaVersion.Should().Be("gmsd:aos:schema:uat-result:v1");
        result.RunId.Should().Be("RUN-001");
        result.TaskId.Should().Be("TASK-001");
        result.Status.Should().Be("passed");
        result.Checks.Should().HaveCount(1);
        result.Checks[0].CriterionId.Should().Be("criterion-001");
        result.Checks[0].Passed.Should().BeTrue();
    }

    [Fact]
    public void UatResult_CheckRecord_HasAllRequiredFields()
    {
        var check = new UatCheckRecord
        {
            CriterionId = "criterion-001",
            Passed = true,
            Message = "Check passed",
            CheckType = "file-exists",
            TargetPath = "src/test.txt",
            Expected = "file exists",
            Actual = "file exists"
        };

        var json = JsonSerializer.Serialize(check, JsonOptions);

        json.Should().Contain("\"criterionId\"");
        json.Should().Contain("\"passed\"");
        json.Should().Contain("\"message\"");
        json.Should().Contain("\"checkType\"");
    }

    [Fact]
    public void UatSpec_Serializes_WithCorrectSchemaVersion()
    {
        var spec = new UatSpec
        {
            SchemaVersion = "gmsd:aos:schema:uat-spec:v1",
            TaskId = "TASK-001",
            Timestamp = DateTimeOffset.Parse("2026-02-06T12:00:00Z"),
            Criteria = new[]
            {
                new UatCriterionRecord
                {
                    Id = "criterion-001",
                    Description = "File should exist",
                    CheckType = "file-exists",
                    TargetPath = "src/test.txt",
                    IsRequired = true
                }
            }.AsReadOnly()
        };

        var json = JsonSerializer.Serialize(spec, JsonOptions);

        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"taskId\"");
        json.Should().Contain("\"timestamp\"");
        json.Should().Contain("\"criteria\"");
        json.Should().Contain("gmsd:aos:schema:uat-spec:v1");
    }

    [Fact]
    public void Issue_Serializes_WithCorrectSchemaVersion()
    {
        var issue = new Gmsd.Agents.Execution.Verification.Issues.Issue
        {
            SchemaVersion = "gmsd:aos:schema:issue:v1",
            Id = "ISS-0001",
            Scope = "src/test.txt",
            Repro = "Run UAT verification for TASK-001",
            Expected = "file exists",
            Actual = "file not found",
            Severity = "high",
            ParentUatId = "criterion-001",
            TaskId = "TASK-001",
            RunId = "RUN-001",
            Timestamp = DateTimeOffset.Parse("2026-02-06T12:00:00Z"),
            DedupHash = "abc123"
        };

        var json = JsonSerializer.Serialize(issue, JsonOptions);

        // Verify all required fields
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"id\"");
        json.Should().Contain("\"scope\"");
        json.Should().Contain("\"repro\"");
        json.Should().Contain("\"expected\"");
        json.Should().Contain("\"actual\"");
        json.Should().Contain("\"severity\"");
        json.Should().Contain("\"parentUatId\"");
        json.Should().Contain("\"taskId\"");
        json.Should().Contain("\"runId\"");
        json.Should().Contain("\"timestamp\"");
        json.Should().Contain("\"dedupHash\"");

        json.Should().Contain("gmsd:aos:schema:issue:v1");
    }

    [Fact]
    public void UatResult_Status_ValidValues()
    {
        var validStatuses = new[] { "passed", "failed", "inconclusive" };

        foreach (var status in validStatuses)
        {
            var result = new UatResult
            {
                SchemaVersion = "gmsd:aos:schema:uat-result:v1",
                RunId = "RUN-001",
                TaskId = "TASK-001",
                Status = status,
                Timestamp = DateTimeOffset.UtcNow,
                Checks = Array.Empty<UatCheckRecord>().AsReadOnly()
            };

            var json = JsonSerializer.Serialize(result, JsonOptions);
            json.Should().Contain($"\"status\": \"{status}\"");
        }
    }

    [Fact]
    public void UatResult_WithEmptyChecks_SerializesCorrectly()
    {
        var result = new UatResult
        {
            SchemaVersion = "gmsd:aos:schema:uat-result:v1",
            RunId = "RUN-001",
            TaskId = "TASK-001",
            Status = "passed",
            Timestamp = DateTimeOffset.UtcNow,
            Checks = Array.Empty<UatCheckRecord>().AsReadOnly()
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);

        json.Should().Contain("\"checks\": []");
    }

    [Fact]
    public void UatResult_WithMultipleChecks_SerializesAll()
    {
        var result = new UatResult
        {
            SchemaVersion = "gmsd:aos:schema:uat-result:v1",
            RunId = "RUN-001",
            TaskId = "TASK-001",
            Status = "failed",
            Timestamp = DateTimeOffset.UtcNow,
            Checks = new[]
            {
                new UatCheckRecord
                {
                    CriterionId = "criterion-001",
                    Passed = true,
                    Message = "Check 1 passed",
                    CheckType = "file-exists"
                },
                new UatCheckRecord
                {
                    CriterionId = "criterion-002",
                    Passed = false,
                    Message = "Check 2 failed",
                    CheckType = "content-contains",
                    Expected = "expected content",
                    Actual = "content not found"
                }
            }.AsReadOnly()
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var deserialized = JsonSerializer.Deserialize<UatResult>(json, JsonOptions);

        deserialized.Should().NotBeNull();
        deserialized!.Checks.Should().HaveCount(2);
        deserialized.Checks[0].CriterionId.Should().Be("criterion-001");
        deserialized.Checks[1].CriterionId.Should().Be("criterion-002");
    }

    [Fact]
    public void UatResult_Timestamp_IsIso8601Format()
    {
        var timestamp = DateTimeOffset.Parse("2026-02-06T12:30:45+05:00");
        var result = new UatResult
        {
            SchemaVersion = "gmsd:aos:schema:uat-result:v1",
            RunId = "RUN-001",
            TaskId = "TASK-001",
            Status = "passed",
            Timestamp = timestamp,
            Checks = Array.Empty<UatCheckRecord>().AsReadOnly()
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);

        // Should contain ISO 8601 formatted timestamp with offset
        json.Should().Contain("2026-02-06");
        json.Should().Contain("12:30:45");
    }

    [Fact]
    public void AcceptanceCriterion_MapsToCheckTypes()
    {
        var checkTypes = new[]
        {
            UatCheckTypes.FileExists,
            UatCheckTypes.ContentContains,
            UatCheckTypes.BuildSucceeds,
            UatCheckTypes.TestPasses
        };

        foreach (var checkType in checkTypes)
        {
            var criterion = new AcceptanceCriterion
            {
                Id = "test-001",
                Description = $"Test {checkType}",
                CheckType = checkType,
                IsRequired = true
            };

            var json = JsonSerializer.Serialize(criterion, JsonOptions);
            json.Should().Contain($"\"checkType\": \"{checkType}\"");
        }
    }
}
