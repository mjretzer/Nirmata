using System.Text.Json;
using Gmsd.Agents.Execution.Validation;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Validation;

public class CanonicalSchemaValidationTests
{
    private const string AosRootPath = "test-aos-root";

    [Fact]
    public void TaskPlan_WithCanonicalSchema_ShouldBeValid()
    {
        // Arrange
        var json = @"
{
  ""schemaVersion"": 1,
  ""taskId"": ""TSK-0001"",
  ""title"": ""Test Task"",
  ""description"": ""Testing canonical schema validation"",
  ""fileScopes"": [
    {
      ""path"": ""src/test.txt"",
      ""scopeType"": ""read""
    }
  ]
}";
        // Act
        // Note: ArtifactContractValidator currently only has ValidateTaskPlan
        var result = ArtifactContractValidator.ValidateTaskPlan("test/plan.json", json, AosRootPath, "test-suite");

        // Assert
        Assert.True(result.IsValid, result.Message);
    }

    [Fact]
    public void TaskPlan_MissingSchemaVersion_ShouldBeInvalid()
    {
        // Arrange
        var json = @"
{
  ""taskId"": ""TSK-0001"",
  ""title"": ""Test Task"",
  ""description"": ""Testing missing version"",
  ""fileScopes"": []
}";
        // Act
        var result = ArtifactContractValidator.ValidateTaskPlan("test/plan.json", json, AosRootPath, "test-suite");

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains(result.Errors, e => e.Message.Contains("required", StringComparison.OrdinalIgnoreCase) || e.Message.Contains("schemaVersion", StringComparison.OrdinalIgnoreCase));
    }
}
