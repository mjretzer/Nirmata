using nirmata.Agents.Execution.Planning.RoadmapModifier;
using Xunit;

namespace nirmata.Agents.Tests.Execution.Planning.RoadmapModifier;

public class RoadmapModifyResultTests
{
    [Fact]
    public void SuccessResult_SetsCorrectStatus()
    {
        var result = RoadmapModifyResult.SuccessResult(RoadmapModifyOperation.Insert, "PH-0001");

        Assert.True(result.IsSuccess);
        Assert.False(result.IsBlocked);
        Assert.Equal(RoadmapModifyOperation.Insert, result.Operation);
        Assert.Equal("PH-0001", result.AffectedPhaseId);
    }

    [Fact]
    public void BlockedResult_SetsCorrectStatus()
    {
        var result = RoadmapModifyResult.BlockedResult("PH-0001", "Active phase cannot be removed", "ISS-1234");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsBlocked);
        Assert.Equal(RoadmapModifyOperation.Remove, result.Operation);
        Assert.Equal("PH-0001", result.AffectedPhaseId);
        Assert.Equal("Active phase cannot be removed", result.BlockerReason);
        Assert.Equal("ISS-1234", result.BlockerIssueId);
    }

    [Fact]
    public void FailedResult_SetsCorrectStatus()
    {
        var result = RoadmapModifyResult.FailedResult("Something went wrong", "ERROR_CODE");

        Assert.False(result.IsSuccess);
        Assert.False(result.IsBlocked);
        Assert.Equal("Something went wrong", result.ErrorMessage);
        Assert.Equal("ERROR_CODE", result.ErrorCode);
    }

    [Fact]
    public void Duration_CalculatesCorrectly()
    {
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var completedAt = DateTimeOffset.UtcNow;

        var result = new RoadmapModifyResult
        {
            StartedAt = startedAt,
            CompletedAt = completedAt
        };

        Assert.True(result.Duration.TotalMinutes >= 4.9 && result.Duration.TotalMinutes <= 5.1);
    }
}

public class RoadmapModifyRequestTests
{
    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        var request = new RoadmapModifyRequest();

        Assert.Equal(RoadmapModifyOperation.Insert, request.Operation);
        Assert.Equal(InsertPosition.AtEnd, request.Position);
        Assert.False(request.Force);
        Assert.Empty(request.RoadmapSpecPath);
        Assert.Empty(request.StatePath);
    }
}

public class GateCheckResultTests
{
    [Fact]
    public void Allowed_ReturnsAllowedResult()
    {
        var result = GateCheckResult.Allowed();

        Assert.True(result.IsAllowed);
        Assert.Null(result.DenialReason);
    }

    [Fact]
    public void Denied_ReturnsDeniedResult()
    {
        var result = GateCheckResult.Denied("Test reason");

        Assert.False(result.IsAllowed);
        Assert.Equal("Test reason", result.DenialReason);
    }
}
