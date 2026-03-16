using nirmata.Agents.Execution.Planning.RoadmapModifier;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Planning.RoadmapModifier;

public class RoadmapRenumbererTests
{
    private readonly RoadmapRenumberer _renumberer = new();

    [Fact]
    public void GenerateNextPhaseId_WithEmptyList_ReturnsPH0001()
    {
        var result = _renumberer.GenerateNextPhaseId(Array.Empty<string>());
        Assert.Equal("PH-0001", result);
    }

    [Fact]
    public void GenerateNextPhaseId_WithExistingPhases_ReturnsNextInSequence()
    {
        var existing = new[] { "PH-0001", "PH-0002", "PH-0003" };
        var result = _renumberer.GenerateNextPhaseId(existing);
        Assert.Equal("PH-0004", result);
    }

    [Fact]
    public void GenerateNextPhaseId_WithGaps_ReturnsMaxPlusOne()
    {
        var existing = new[] { "PH-0001", "PH-0005", "PH-0003" };
        var result = _renumberer.GenerateNextPhaseId(existing);
        Assert.Equal("PH-0006", result);
    }

    [Fact]
    public void RenumberPhases_AssignsConsecutiveIds()
    {
        var phases = new List<PhaseReference>
        {
            new() { PhaseId = "PH-0005", MilestoneId = "MS-0001", SequenceOrder = 1, Name = "Phase A" },
            new() { PhaseId = "PH-0001", MilestoneId = "MS-0001", SequenceOrder = 2, Name = "Phase B" },
            new() { PhaseId = "PH-0003", MilestoneId = "MS-0001", SequenceOrder = 3, Name = "Phase C" }
        };

        var mapping = _renumberer.RenumberPhases(phases);

        Assert.Equal("PH-0001", mapping["PH-0005"]);
        Assert.Equal("PH-0002", mapping["PH-0001"]);
        Assert.Equal("PH-0003", mapping["PH-0003"]);
    }

    [Fact]
    public void ExtractSequenceNumber_ValidId_ReturnsNumber()
    {
        Assert.Equal(1, _renumberer.ExtractSequenceNumber("PH-0001"));
        Assert.Equal(42, _renumberer.ExtractSequenceNumber("PH-0042"));
        Assert.Equal(9999, _renumberer.ExtractSequenceNumber("PH-9999"));
    }

    [Fact]
    public void ExtractSequenceNumber_InvalidId_ReturnsMinusOne()
    {
        Assert.Equal(-1, _renumberer.ExtractSequenceNumber(""));
        Assert.Equal(-1, _renumberer.ExtractSequenceNumber("INVALID"));
        Assert.Equal(-1, _renumberer.ExtractSequenceNumber("PH-1"));
        Assert.Equal(-1, _renumberer.ExtractSequenceNumber("PH-12345"));
    }

    [Fact]
    public void IsValidPhaseIdFormat_ValidIds_ReturnsTrue()
    {
        Assert.True(_renumberer.IsValidPhaseIdFormat("PH-0001"));
        Assert.True(_renumberer.IsValidPhaseIdFormat("PH-9999"));
        Assert.True(_renumberer.IsValidPhaseIdFormat("PH-1234"));
    }

    [Fact]
    public void IsValidPhaseIdFormat_InvalidIds_ReturnsFalse()
    {
        Assert.False(_renumberer.IsValidPhaseIdFormat(""));
        Assert.False(_renumberer.IsValidPhaseIdFormat("PH-1"));
        Assert.False(_renumberer.IsValidPhaseIdFormat("PH-12345"));
        Assert.False(_renumberer.IsValidPhaseIdFormat("INVALID"));
        Assert.False(_renumberer.IsValidPhaseIdFormat("ph-0001")); // lowercase
    }
}
