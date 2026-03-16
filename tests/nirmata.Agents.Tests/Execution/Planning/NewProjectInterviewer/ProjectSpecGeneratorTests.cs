using FluentAssertions;
using nirmata.Agents.Execution.Planning;
using Xunit;

namespace nirmata.Agents.Tests.Workflows.Planning;

public class ProjectSpecGeneratorTests
{
    private readonly ProjectSpecGenerator _sut = new();

    [Fact]
    public void GenerateFromSession_WithValidSession_ReturnsProjectSpecification()
    {
        // Arrange
        var session = new InterviewSession
        {
            SessionId = "test-session-123",
            ProjectDraft = new ProjectSpecDraft
            {
                Name = "My Project",
                Description = "A sample project description",
                TechnologyStack = ".NET/C#",
                Goals = ["Goal 1", "Goal 2"],
                TargetAudience = "Developers",
                KeyFeatures = ["Feature A", "Feature B"],
                Constraints = ["Must be fast"],
                Assumptions = ["Cloud deployment"]
            },
            QAPairs =
            [
                new InterviewQAPair
                {
                    Question = "What is the project name?",
                    Answer = "My Project",
                    Phase = InterviewPhase.Discovery
                }
            ]
        };

        // Act
        var spec = _sut.GenerateFromSession(session);

        // Assert
        spec.Should().NotBeNull();
        spec.Name.Should().Be("My Project");
        spec.Description.Should().Be("A sample project description");
        spec.TechnologyStack.Should().Be(".NET/C#");
        spec.Goals.Should().HaveCount(2);
        spec.Goals.Should().Contain("Goal 1");
        spec.Goals.Should().Contain("Goal 2");
        spec.TargetAudience.Should().Be("Developers");
        spec.KeyFeatures.Should().HaveCount(2);
        spec.Constraints.Should().HaveCount(1);
        spec.Assumptions.Should().HaveCount(1);
    }

    [Fact]
    public void GenerateFromSession_SetsCorrectSchema()
    {
        // Arrange
        var session = new InterviewSession
        {
            ProjectDraft = new ProjectSpecDraft
            {
                Name = "Test",
                Description = "Test description"
            }
        };

        // Act
        var spec = _sut.GenerateFromSession(session);

        // Assert
        spec.Schema.Should().Be("nirmata:aos:schema:project:v1");
    }

    [Fact]
    public void GenerateFromSession_PopulatesMetadata()
    {
        // Arrange
        var session = new InterviewSession
        {
            SessionId = "session-456",
            ProjectDraft = new ProjectSpecDraft
            {
                Name = "Test",
                Description = "Test description"
            },
            QAPairs =
            [
                new InterviewQAPair { Question = "Q1", Answer = "A1", Phase = InterviewPhase.Discovery },
                new InterviewQAPair { Question = "Q2", Answer = "A2", Phase = InterviewPhase.Clarification }
            ]
        };

        // Act
        var spec = _sut.GenerateFromSession(session);

        // Assert
        spec.Metadata.Should().ContainKey("generatedFromInterview");
        spec.Metadata["generatedFromInterview"].Should().Be(true);
        spec.Metadata.Should().ContainKey("interviewSessionId");
        spec.Metadata["interviewSessionId"].Should().Be("session-456");
        spec.Metadata.Should().ContainKey("interviewCompletedAt");
        spec.Metadata.Should().ContainKey("qaPairCount");
        spec.Metadata["qaPairCount"].Should().Be(2);
    }

    [Fact]
    public void GenerateFromSession_SessionIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _sut.GenerateFromSession(null!));
    }

    [Fact]
    public void GenerateFromSession_ProjectDraftIsNull_ThrowsInvalidOperationException()
    {
        // Arrange
        var session = new InterviewSession
        {
            ProjectDraft = null
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => _sut.GenerateFromSession(session));
    }

    [Fact]
    public void GenerateFromSession_WithMinimalDraft_UsesDefaultValues()
    {
        // Arrange
        var session = new InterviewSession
        {
            ProjectDraft = new ProjectSpecDraft
            {
                Name = null,
                Description = null
            }
        };

        // Act
        var spec = _sut.GenerateFromSession(session);

        // Assert
        spec.Name.Should().Be("Untitled Project");
        spec.Description.Should().Be("No description provided.");
    }

    [Fact]
    public void GenerateFromSession_PreservesOptionalNullValues()
    {
        // Arrange
        var session = new InterviewSession
        {
            ProjectDraft = new ProjectSpecDraft
            {
                Name = "Test",
                Description = "Test description",
                TechnologyStack = null,
                TargetAudience = null
            }
        };

        // Act
        var spec = _sut.GenerateFromSession(session);

        // Assert
        spec.TechnologyStack.Should().BeNull();
        spec.TargetAudience.Should().BeNull();
    }

    [Fact]
    public void SerializeToJson_ProducesValidJson()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "Test Project",
            Description = "Test Description",
            TechnologyStack = ".NET",
            Goals = ["Goal 1"]
        };

        // Act
        var json = _sut.SerializeToJson(spec);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"project\"");
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"description\"");
        json.Should().Contain("Test Project");
        json.Should().Contain("Test Project");
    }

    [Fact]
    public void SerializeToJson_UsesCamelCaseNaming()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "Test",
            Description = "Test"
        };

        // Act
        var json = _sut.SerializeToJson(spec);

        // Assert
        json.Should().Contain("\"schemaVersion\"");
        json.Should().Contain("\"project\"");
        json.Should().Contain("\"name\"");
        json.Should().Contain("\"description\"");
        json.Should().NotContain("\"Schema\"");
        json.Should().NotContain("\"Name\"");
    }

    [Fact]
    public void SerializeToJson_UsesLfLineEndings()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "Test",
            Description = "Test"
        };

        // Act
        var json = _sut.SerializeToJson(spec);

        // Assert
        json.Should().NotContain("\r\n");
    }

    [Fact]
    public void SerializeToJson_IsIndented()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "Test",
            Description = "Test"
        };

        // Act
        var json = _sut.SerializeToJson(spec);

        // Assert
        json.Should().Contain("\n  ");
    }

    [Fact]
    public void SerializeToJson_SpecIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _sut.SerializeToJson(null!));
    }

    [Fact]
    public void ParseFromJson_ValidNestedJson_ReturnsProjectSpecification()
    {
        // Arrange - nested schema format
        var json = """
            {
                "schemaVersion": 1,
                "project": {
                    "name": "Parsed Project",
                    "description": "Parsed description"
                }
            }
            """;

        // Act
        var spec = _sut.ParseFromJson(json);

        // Assert
        spec.Should().NotBeNull();
        spec!.Name.Should().Be("Parsed Project");
        spec.Description.Should().Be("Parsed description");
    }

    [Fact]
    public void ParseFromJson_LegacyFlatJson_ReturnsProjectSpecification()
    {
        // Arrange - legacy flat format
        var json = """
            {
                "schema": "nirmata:aos:schema:project:v1",
                "name": "Parsed Project",
                "description": "Parsed description",
                "technologyStack": "Python",
                "goals": ["Goal 1", "Goal 2"]
            }
            """;

        // Act
        var spec = _sut.ParseFromJson(json);

        // Assert
        spec.Should().NotBeNull();
        spec!.Name.Should().Be("Parsed Project");
        spec.Description.Should().Be("Parsed description");
        spec.TechnologyStack.Should().Be("Python");
        spec.Goals.Should().HaveCount(2);
    }

    [Fact]
    public void ParseFromJson_NullOrEmpty_ReturnsNull()
    {
        // Act & Assert
        _sut.ParseFromJson(null!).Should().BeNull();
        _sut.ParseFromJson("").Should().BeNull();
        _sut.ParseFromJson("   ").Should().BeNull();
    }

    [Fact]
    public void ParseFromJson_InvalidJson_ReturnsNull()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act
        var spec = _sut.ParseFromJson(invalidJson);

        // Assert
        spec.Should().BeNull();
    }

    [Fact]
    public void ParseFromJson_MissingOptionalFields_ReturnsSpecWithDefaults()
    {
        // Arrange - nested schema format with minimal fields
        var json = """
            {
                "schemaVersion": 1,
                "project": {
                    "name": "Minimal",
                    "description": "Minimal project"
                }
            }
            """;

        // Act
        var spec = _sut.ParseFromJson(json);

        // Assert
        spec.Should().NotBeNull();
        spec!.Goals.Should().BeEmpty();
        spec.KeyFeatures.Should().BeEmpty();
        spec.Constraints.Should().BeEmpty();
        spec.Assumptions.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidSpec_ReturnsSuccess()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "Valid Project",
            Description = "Valid description"
        };

        // Act
        var result = _sut.Validate(spec);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_SpecIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _sut.Validate(null!));
    }

    [Fact]
    public void Validate_MissingSchema_ReturnsFailure()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "",
            Name = "Test",
            Description = "Test"
        };

        // Act
        var result = _sut.Validate(spec);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Schema is required.");
    }

    [Fact]
    public void Validate_WrongSchema_ReturnsFailure()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "wrong:schema",
            Name = "Test",
            Description = "Test"
        };

        // Act
        var result = _sut.Validate(spec);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Unexpected schema"));
    }

    [Fact]
    public void Validate_MissingName_ReturnsFailure()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "",
            Description = "Test"
        };

        // Act
        var result = _sut.Validate(spec);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Project name is required.");
    }

    [Fact]
    public void Validate_MissingDescription_ReturnsFailure()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "Test",
            Description = ""
        };

        // Act
        var result = _sut.Validate(spec);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Project description is required.");
    }

    [Fact]
    public void Validate_MultipleErrors_ReturnsAllErrors()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "",
            Name = "",
            Description = ""
        };

        // Act
        var result = _sut.Validate(spec);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(1);
    }

    [Fact]
    public void Validate_SchemaIsCaseInsensitive()
    {
        // Arrange
        var spec = new ProjectSpecification
        {
            Schema = "nirmata:AOS:SCHEMA:PROJECT:V1",
            Name = "Test",
            Description = "Test"
        };

        // Act
        var result = _sut.Validate(spec);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void SpecValidationResult_Success_CreatesValidResult()
    {
        // Act
        var result = SpecValidationResult.Success();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SpecValidationResult_Failure_CreatesInvalidResultWithErrors()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };

        // Act
        var result = SpecValidationResult.Failure(errors);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain("Error 1");
        result.Errors.Should().Contain("Error 2");
    }

    [Fact]
    public void SerializeAndParse_RoundTrip_PreservesCoreData()
    {
        // Arrange - note: JSON format only stores name/description, not extended fields
        var original = new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = "Round Trip Test",
            Description = "Testing round trip serialization",
            TechnologyStack = "C#",  // This won't be preserved in JSON
            Goals = ["Goal 1", "Goal 2"]  // These won't be preserved in JSON
        };

        // Act
        var json = _sut.SerializeToJson(original);
        var parsed = _sut.ParseFromJson(json);

        // Assert - only core fields are preserved in the JSON schema format
        parsed.Should().NotBeNull();
        parsed!.Schema.Should().Be(original.Schema);
        parsed.Name.Should().Be(original.Name);
        parsed.Description.Should().Be(original.Description);
        // Extended fields are not stored in the schema-compliant JSON format
        parsed.TechnologyStack.Should().BeNull();
        parsed.Goals.Should().BeEmpty();
    }
}
