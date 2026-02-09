using FluentAssertions;
using Gmsd.Agents.Execution.Planning.PhasePlanner;
using Gmsd.Agents.Execution.Planning.PhasePlanner.Assumptions;
using Gmsd.Aos.Public;
using Moq;
using Xunit;

namespace Gmsd.Agents.Tests.Execution.Planning.PhasePlanner.Assumptions;

public class PhaseAssumptionListerTests
{
    private readonly Mock<IWorkspace> _workspaceMock;
    private readonly PhaseAssumptionLister _sut;

    public PhaseAssumptionListerTests()
    {
        _workspaceMock = new Mock<IWorkspace>();
        _workspaceMock.Setup(x => x.AosRootPath).Returns(Path.Combine(Path.GetTempPath(), ".aos"));
        _sut = new PhaseAssumptionLister(_workspaceMock.Object);
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_WithNullBrief_ThrowsArgumentNullException()
    {
        // Arrange
        var taskPlan = CreateValidTaskPlan();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ExtractAssumptionsAsync(null!, taskPlan, "RUN-001"));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_WithNullTaskPlan_ThrowsArgumentNullException()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.ExtractAssumptionsAsync(brief, null!, "RUN-001"));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_WithNullRunId_ThrowsArgumentException()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var taskPlan = CreateValidTaskPlan();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ExtractAssumptionsAsync(brief, taskPlan, null!));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_WithEmptyRunId_ThrowsArgumentException()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var taskPlan = CreateValidTaskPlan();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.ExtractAssumptionsAsync(brief, taskPlan, string.Empty));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_WithValidInputs_ReturnsAssumptions()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var taskPlan = CreateValidTaskPlan();

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");

        // Assert
        result.Should().NotBeNull();
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_AssumptionsHaveIds()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var taskPlan = CreateValidTaskPlan();

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");

        // Assert
        result.Should().OnlyContain(a => !string.IsNullOrEmpty(a.AssumptionId));
        result.Should().OnlyContain(a => a.AssumptionId.StartsWith("ASM-"));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_AssumptionsHavePhaseId()
    {
        // Arrange
        var brief = CreateValidPhaseBrief("PH-TEST-001");
        var taskPlan = CreateValidTaskPlan("PH-TEST-001");

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");

        // Assert
        result.Should().OnlyContain(a => a.PhaseId == "PH-TEST-001");
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_AssumptionsHaveCategories()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var taskPlan = CreateValidTaskPlan();

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");

        // Assert
        result.Should().OnlyContain(a => !string.IsNullOrEmpty(a.Category));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_AssumptionsHaveStatements()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var taskPlan = CreateValidTaskPlan();

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");

        // Assert
        result.Should().OnlyContain(a => !string.IsNullOrEmpty(a.Statement));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_AssumptionsHaveRationale()
    {
        // Arrange
        var brief = CreateValidPhaseBrief();
        var taskPlan = CreateValidTaskPlan();

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");

        // Assert
        result.Should().OnlyContain(a => !string.IsNullOrEmpty(a.Rationale));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_AssumptionsHaveIdentifiedAtTimestamp()
    {
        // Arrange
        var beforeTest = DateTimeOffset.UtcNow;
        var brief = CreateValidPhaseBrief();
        var taskPlan = CreateValidTaskPlan();

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");
        var afterTest = DateTimeOffset.UtcNow;

        // Assert
        result.Should().OnlyContain(a => a.IdentifiedAt >= beforeTest && a.IdentifiedAt <= afterTest);
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_WithScopeInScope_ExtractsScopeAssumptions()
    {
        // Arrange
        var brief = CreateValidPhaseBrief(inScope: new[] { "Feature A", "Feature B" });
        var taskPlan = CreateValidTaskPlan();

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");

        // Assert
        result.Should().Contain(a => a.Statement.Contains("scope"));
    }

    [Fact]
    public async Task ExtractAssumptionsAsync_WithRelevantFiles_ExtractsFileAssumptions()
    {
        // Arrange
        var brief = CreateValidPhaseBrief(relevantFiles: new[]
        {
            new CodeFileReference { FilePath = "/test/file.cs", RelativePath = "file.cs", FileType = "implementation" }
        });
        var taskPlan = CreateValidTaskPlan();

        // Act
        var result = await _sut.ExtractAssumptionsAsync(brief, taskPlan, "RUN-001");

        // Assert
        result.Should().Contain(a => a.Statement.Contains("file") || a.Statement.Contains("exist"));
    }

    [Fact]
    public async Task GenerateAssumptionsDocumentAsync_WithNullAssumptions_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _sut.GenerateAssumptionsDocumentAsync(null!, "RUN-001"));
    }

    [Fact]
    public async Task GenerateAssumptionsDocumentAsync_WithNullRunId_ThrowsArgumentException()
    {
        // Arrange
        var assumptions = new List<PhaseAssumption>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GenerateAssumptionsDocumentAsync(assumptions, null!));
    }

    [Fact]
    public async Task GenerateAssumptionsDocumentAsync_WithEmptyRunId_ThrowsArgumentException()
    {
        // Arrange
        var assumptions = new List<PhaseAssumption>();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _sut.GenerateAssumptionsDocumentAsync(assumptions, string.Empty));
    }

    [Fact]
    public async Task GenerateAssumptionsDocumentAsync_WithEmptyAssumptions_ReturnsEmptyPath()
    {
        // Arrange
        var assumptions = new List<PhaseAssumption>();

        // Act
        var result = await _sut.GenerateAssumptionsDocumentAsync(assumptions, "RUN-001");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAssumptionsDocumentAsync_WithAssumptions_ReturnsPath()
    {
        // Arrange
        var assumptions = new List<PhaseAssumption>
        {
            CreateAssumption("ASM-001", "technical", "Test assumption", "Test rationale")
        };

        // Act
        var result = await _sut.GenerateAssumptionsDocumentAsync(assumptions, "RUN-001");

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("assumptions.md");
    }

    [Fact]
    public async Task GenerateAssumptionsDocumentAsync_CreatesFile()
    {
        // Arrange
        var assumptions = new List<PhaseAssumption>
        {
            CreateAssumption("ASM-001", "technical", "Test assumption", "Test rationale")
        };
        var runId = $"RUN-{Guid.NewGuid():N}";

        // Act
        var result = await _sut.GenerateAssumptionsDocumentAsync(assumptions, runId);

        // Assert
        File.Exists(result).Should().BeTrue();

        // Cleanup
        try { File.Delete(result); } catch { }
    }

    [Fact]
    public async Task GenerateAssumptionsDocumentAsync_ContainsAssumptionContent()
    {
        // Arrange
        var assumptions = new List<PhaseAssumption>
        {
            CreateAssumption("ASM-001", "technical", "Files exist assumption", "Rationale text")
        };
        var runId = $"RUN-{Guid.NewGuid():N}";

        // Act
        var result = await _sut.GenerateAssumptionsDocumentAsync(assumptions, runId);
        var content = await File.ReadAllTextAsync(result);

        // Assert
        content.Should().Contain("Assumptions");
        content.Should().Contain("ASM-001");
        content.Should().Contain("Files exist assumption");
        content.Should().Contain("Rationale text");

        // Cleanup
        try { File.Delete(result); } catch { }
    }

    private static PhaseBrief CreateValidPhaseBrief(
        string phaseId = "PH-001",
        string[]? inScope = null,
        string[]? outOfScope = null,
        CodeFileReference[]? relevantFiles = null)
    {
        return new PhaseBrief
        {
            PhaseId = phaseId,
            PhaseName = "Test Phase",
            Description = "Test description",
            MilestoneId = "MS-001",
            Scope = new PhaseScope
            {
                InScope = inScope ?? Array.Empty<string>(),
                OutOfScope = outOfScope ?? Array.Empty<string>(),
                Boundaries = Array.Empty<string>()
            },
            RelevantFiles = relevantFiles ?? Array.Empty<CodeFileReference>(),
            ProjectContext = new ProjectContext
            {
                TechnologyStack = ".NET/C#"
            }
        };
    }

    private static TaskPlan CreateValidTaskPlan(string phaseId = "PH-001")
    {
        return new TaskPlan
        {
            PlanId = "PLAN-001",
            PhaseId = phaseId,
            RunId = "RUN-001",
            IsValid = true,
            Tasks = new List<TaskSpecification>
            {
                new()
                {
                    TaskId = "TSK-001",
                    PhaseId = phaseId,
                    Title = "Test Task",
                    Description = "Test task description",
                    FileScopes = new List<FileScope>
                    {
                        new() { RelativePath = "test.cs", ScopeType = "modify" }
                    },
                    VerificationSteps = new List<VerificationStep>
                    {
                        new() { VerificationType = "test", Description = "Run tests" }
                    }
                }
            }
        };
    }

    private static PhaseAssumption CreateAssumption(string id, string category, string statement, string rationale)
    {
        return new PhaseAssumption
        {
            AssumptionId = id,
            PhaseId = "PH-001",
            Category = category,
            Statement = statement,
            Rationale = rationale,
            Source = "test"
        };
    }
}
