using nirmata.Agents.Execution.Planning.RoadmapModifier;
using nirmata.Agents.Models.Results;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Planning.RoadmapModifier;

public class RoadmapValidatorTests
{
    private readonly RoadmapValidator _validator = new();

    [Fact]
    public void ValidateRoadmap_ValidPhases_ReturnsValid()
    {
        var phases = new List<PhaseSpec>
        {
            new() { PhaseId = "PH-0001", MilestoneId = "MS-0001", SequenceOrder = 1, Name = "Phase 1" },
            new() { PhaseId = "PH-0002", MilestoneId = "MS-0001", SequenceOrder = 2, Name = "Phase 2" },
            new() { PhaseId = "PH-0003", MilestoneId = "MS-0001", SequenceOrder = 3, Name = "Phase 3" }
        };

        var milestones = new List<MilestoneSpec>
        {
            new() { MilestoneId = "MS-0001", Name = "Milestone 1", PhaseIds = new[] { "PH-0001", "PH-0002", "PH-0003" }.ToList() }
        };

        var (isValid, errors) = _validator.ValidateRoadmap(phases, milestones);

        Assert.True(isValid);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateRoadmap_InvalidPhaseIdFormat_ReturnsError()
    {
        var phases = new List<PhaseSpec>
        {
            new() { PhaseId = "INVALID", MilestoneId = "MS-0001", SequenceOrder = 1, Name = "Phase 1" }
        };

        var milestones = new List<MilestoneSpec>
        {
            new() { MilestoneId = "MS-0001", Name = "Milestone 1", PhaseIds = new[] { "INVALID" }.ToList() }
        };

        var (isValid, errors) = _validator.ValidateRoadmap(phases, milestones);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Invalid phase ID format"));
    }

    [Fact]
    public void ValidateRoadmap_DuplicatePhaseIds_ReturnsError()
    {
        var phases = new List<PhaseSpec>
        {
            new() { PhaseId = "PH-0001", MilestoneId = "MS-0001", SequenceOrder = 1, Name = "Phase 1A" },
            new() { PhaseId = "PH-0001", MilestoneId = "MS-0001", SequenceOrder = 2, Name = "Phase 1B" }
        };

        var milestones = new List<MilestoneSpec>
        {
            new() { MilestoneId = "MS-0001", Name = "Milestone 1", PhaseIds = new[] { "PH-0001" }.ToList() }
        };

        var (isValid, errors) = _validator.ValidateRoadmap(phases, milestones);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Duplicate phase ID"));
    }

    [Fact]
    public void ValidateRoadmap_DuplicateSequenceOrders_ReturnsError()
    {
        var phases = new List<PhaseSpec>
        {
            new() { PhaseId = "PH-0001", MilestoneId = "MS-0001", SequenceOrder = 1, Name = "Phase 1" },
            new() { PhaseId = "PH-0002", MilestoneId = "MS-0001", SequenceOrder = 1, Name = "Phase 2" }
        };

        var milestones = new List<MilestoneSpec>
        {
            new() { MilestoneId = "MS-0001", Name = "Milestone 1", PhaseIds = new[] { "PH-0001", "PH-0002" }.ToList() }
        };

        var (isValid, errors) = _validator.ValidateRoadmap(phases, milestones);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Duplicate sequence order"));
    }

    [Fact]
    public void ValidateRoadmap_SequenceGap_ReturnsError()
    {
        var phases = new List<PhaseSpec>
        {
            new() { PhaseId = "PH-0001", MilestoneId = "MS-0001", SequenceOrder = 1, Name = "Phase 1" },
            new() { PhaseId = "PH-0002", MilestoneId = "MS-0001", SequenceOrder = 3, Name = "Phase 3" }
        };

        var milestones = new List<MilestoneSpec>
        {
            new() { MilestoneId = "MS-0001", Name = "Milestone 1", PhaseIds = new[] { "PH-0001", "PH-0002" }.ToList() }
        };

        var (isValid, errors) = _validator.ValidateRoadmap(phases, milestones);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Sequence order gap"));
    }

    [Fact]
    public void ValidateRoadmap_InvalidMilestoneReference_ReturnsError()
    {
        var phases = new List<PhaseSpec>
        {
            new() { PhaseId = "PH-0001", MilestoneId = "MS-INVALID", SequenceOrder = 1, Name = "Phase 1" }
        };

        var milestones = new List<MilestoneSpec>
        {
            new() { MilestoneId = "MS-0001", Name = "Milestone 1", PhaseIds = new[] { "PH-0001" }.ToList() }
        };

        var (isValid, errors) = _validator.ValidateRoadmap(phases, milestones);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("invalid milestone"));
    }

    [Fact]
    public void ValidateRoadmap_OrphanedPhase_ReturnsError()
    {
        var phases = new List<PhaseSpec>
        {
            new() { PhaseId = "PH-0001", MilestoneId = "MS-0001", SequenceOrder = 1, Name = "Phase 1" },
            new() { PhaseId = "PH-0002", MilestoneId = "MS-0001", SequenceOrder = 2, Name = "Orphan Phase" }
        };

        var milestones = new List<MilestoneSpec>
        {
            new() { MilestoneId = "MS-0001", Name = "Milestone 1", PhaseIds = new[] { "PH-0001" }.ToList() }
        };

        var (isValid, errors) = _validator.ValidateRoadmap(phases, milestones);

        Assert.False(isValid);
        Assert.Contains(errors, e => e.Contains("Orphaned phase"));
    }
}
