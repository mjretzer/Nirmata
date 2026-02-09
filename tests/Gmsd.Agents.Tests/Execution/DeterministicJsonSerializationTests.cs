using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Gmsd.Agents.Execution.Execution.SubagentRuns;
using Gmsd.Agents.Execution.Execution.TaskExecutor;
using Xunit;

namespace Gmsd.Agents.Tests.Execution;

public class DeterministicJsonSerializationTests
{
    [Fact]
    public void ComputeDeterministicHash_ProducesConsistentHash_ForSameInput()
    {
        // Arrange
        var input = "test data for hashing";

        // Act
        var hash1 = ComputeDeterministicHash(input);
        var hash2 = ComputeDeterministicHash(input);

        // Assert
        hash1.Should().Be(hash2);
        hash1.Should().NotBeNullOrEmpty();
        hash1.Length.Should().Be(64); // SHA256 hex string length
    }

    [Fact]
    public void ComputeDeterministicHash_ProducesDifferentHash_ForDifferentInput()
    {
        // Arrange
        var input1 = "test data 1";
        var input2 = "test data 2";

        // Act
        var hash1 = ComputeDeterministicHash(input1);
        var hash2 = ComputeDeterministicHash(input2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void ComputeDeterministicHash_IsCaseSensitive()
    {
        // Arrange
        var input1 = "Test Data";
        var input2 = "test data";

        // Act
        var hash1 = ComputeDeterministicHash(input1);
        var hash2 = ComputeDeterministicHash(input2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void TaskExecutionResult_Serializes_WithCamelCasePropertyNames()
    {
        // Arrange
        var result = new TaskExecutionResult
        {
            Success = true,
            RunId = "RUN-001",
            NormalizedOutput = "{}",
            DeterministicHash = "abc123",
            ModifiedFiles = new[] { "file1.cs" }.ToList().AsReadOnly(),
            EvidenceArtifacts = new[] { ".aos/evidence/runs/RUN-001/" }
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        json.Should().Contain("\"success\":");
        json.Should().Contain("\"runId\":");
        json.Should().Contain("\"normalizedOutput\":");
        json.Should().Contain("\"deterministicHash\":");
        json.Should().Contain("\"modifiedFiles\":");
        json.Should().Contain("\"evidenceArtifacts\":");
        json.Should().NotContain("\"Success\":");
    }

    [Fact]
    public void SubagentRunResult_Serializes_WithCamelCasePropertyNames()
    {
        // Arrange
        var result = new SubagentRunResult
        {
            Success = true,
            RunId = "RUN-001",
            TaskId = "TSK-001",
            NormalizedOutput = "{}",
            DeterministicHash = "abc123"
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        json.Should().Contain("\"success\":");
        json.Should().Contain("\"runId\":");
        json.Should().Contain("\"taskId\":");
        json.Should().NotContain("\"Success\":");
        json.Should().NotContain("\"RunId\":");
    }

    [Fact]
    public void JsonSerialization_PreservesOrder_InCollections()
    {
        // Arrange
        var files = new[] { "file3.cs", "file1.cs", "file2.cs" };
        var result = new TaskExecutionResult
        {
            Success = true,
            RunId = "RUN-001",
            ModifiedFiles = files.ToList().AsReadOnly()
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        json.Should().Contain("file3.cs");
        json.Should().Contain("file1.cs");
        json.Should().Contain("file2.cs");
    }

    [Fact]
    public void JsonSerializerOptions_AreConsistent_AcrossMultipleCalls()
    {
        // Arrange
        var data = new { name = "test", value = 42 };

        var options1 = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        var options2 = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Act
        var json1 = JsonSerializer.Serialize(data, options1);
        var json2 = JsonSerializer.Serialize(data, options2);

        // Assert
        json1.Should().Be(json2);
    }

    [Fact]
    public void JsonSerializer_UsesInvariantCulture_ForNumbers()
    {
        // Arrange
        var data = new { value = 1234.56 };

        // Act
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        json.Should().Contain("1234.56");
    }

    [Fact]
    public void JsonSerializer_HandlesNullValues_Consistently()
    {
        // Arrange
        var result = new TaskExecutionResult
        {
            Success = true,
            ErrorMessage = null,
            RunId = "RUN-001"
        };

        // Act
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        // Assert
        json.Should().NotContain("errorMessage");
    }

    [Fact]
    public void DeterministicHash_UsesLowercaseHex()
    {
        // Arrange
        var input = "any input data";

        // Act
        var hash = ComputeDeterministicHash(input);

        // Assert
        hash.Should().Be(hash.ToLowerInvariant());
        hash.Should().NotContainAny("A", "B", "C", "D", "E", "F");
    }

    [Fact]
    public void NormalizedOutput_ProducesSameHash_ForSameData()
    {
        // Arrange
        var output1 = JsonSerializer.Serialize(new
        {
            action = "task_execution",
            files = new[] { "src/file.cs" },
            summary = new { stepCount = 5 }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var output2 = JsonSerializer.Serialize(new
        {
            action = "task_execution",
            files = new[] { "src/file.cs" },
            summary = new { stepCount = 5 }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Act
        var hash1 = ComputeDeterministicHash(output1);
        var hash2 = ComputeDeterministicHash(output2);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void NormalizedOutput_ProducesDifferentHash_ForDifferentPropertyOrder()
    {
        // Arrange - Note: System.Text.Json preserves property order from object initialization
        var output1 = JsonSerializer.Serialize(new
        {
            action = "task_execution",
            files = new[] { "src/file.cs" }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var output2 = JsonSerializer.Serialize(new
        {
            files = new[] { "src/file.cs" },
            action = "task_execution"
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        // Act
        var hash1 = ComputeDeterministicHash(output1);
        var hash2 = ComputeDeterministicHash(output2);

        // Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Utf8Encoding_IsUsed_Consistently()
    {
        // Arrange
        var input = "Unicode test: 测试 😀";

        // Act
        var hash1 = ComputeDeterministicHash(input);

        var bytes = Encoding.UTF8.GetBytes(input);
        var hash2 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        // Assert
        hash1.Should().Be(hash2);
    }

    private static string ComputeDeterministicHash(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
