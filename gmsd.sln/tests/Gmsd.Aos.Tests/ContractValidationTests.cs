using Gmsd.Aos.Contracts.Commands;
using Gmsd.Aos.Contracts.State;
using Xunit;

namespace Gmsd.Aos.Tests;

public class ContractValidationTests
{
    [Fact]
    public void PhasePlan_Validate_ValidPlan_ReturnsTrue()
    {
        // Arrange
        var plan = new PhasePlan
        {
            PlanId = "plan-1",
            PhaseId = "phase-1",
            Tasks = new List<PhaseTask>
            {
                new PhaseTask
                {
                    Id = "TSK-001",
                    Title = "Task 1",
                    Description = "Description for task 1 that is long enough",
                    FileScopes = new List<PhaseFileScope> { new() { Path = "file1.cs" } },
                    VerificationSteps = new List<string> { "Verify it works" }
                }
            }
        };

        // Act
        var result = plan.Validate();

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void PhasePlan_Validate_InvalidPlan_ReturnsFalse()
    {
        // Arrange
        var plan = new PhasePlan
        {
            PlanId = "plan-1",
            PhaseId = "phase-1",
            Tasks = new List<PhaseTask>() // Empty tasks list is invalid
        };

        // Act
        var result = plan.Validate();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("At least one task is required"));
    }

    [Fact]
    public void PhaseTask_Validate_InvalidTask_ReturnsFalse()
    {
        // Arrange
        var plan = new PhasePlan
        {
            PlanId = "plan-1",
            PhaseId = "phase-1",
            Tasks = new List<PhaseTask>
            {
                new PhaseTask
                {
                    Id = "TSK-001",
                    Title = "Short", // Too short
                    Description = "Short", // Too short
                    FileScopes = new List<PhaseFileScope>(),
                    VerificationSteps = new List<string>() // Empty
                }
            }
        };

        // Act
        var result = plan.Validate();

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void FixPlan_Validate_ValidPlan_ReturnsTrue()
    {
        // Arrange
        var plan = new FixPlan
        {
            Fixes = new List<FixEntry>
            {
                new FixEntry
                {
                    IssueId = "ISS-001",
                    Description = "Fixing the issue with valid description",
                    ProposedChanges = new List<ProposedChange>
                    {
                        new ProposedChange { File = "file.cs", ChangeDescription = "Fix bug" }
                    },
                    Tests = new List<string> { "Run TestA" }
                }
            }
        };

        // Act
        var result = plan.Validate();

        // Assert
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CommandIntentProposal_IsValid_ValidProposal_ReturnsTrue()
    {
        // Arrange
        var proposal = new CommandIntentProposal
        {
            SchemaVersion = 1,
            Intent = new CommandIntent { Goal = "Run tests" },
            Command = "/run-tests",
            Group = "run",
            Rationale = "Needed to verify changes",
            ExpectedOutcome = "Tests pass successfully"
        };

        // Act
        var isValid = proposal.IsValid(out var errors);

        // Assert
        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void CommandIntentProposal_IsValid_InvalidCommandFormat_ReturnsFalse()
    {
        // Arrange
        var proposal = new CommandIntentProposal
        {
            SchemaVersion = 1,
            Intent = new CommandIntent { Goal = "Run tests" },
            Command = "run-tests", // Missing leading slash
            Group = "run",
            Rationale = "Needed to verify changes",
            ExpectedOutcome = "Tests pass successfully"
        };

        // Act
        var isValid = proposal.IsValid(out var errors);

        // Assert
        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Command must start with /"));
    }
}
