using nirmata.Agents.Execution.Planning;
using nirmata.Agents.Models.Results;
using nirmata.Agents.Models.Runtime;

namespace nirmata.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IRoadmapper for testing.
/// Returns a canned roadmap result for testing purposes.
/// </summary>
public sealed class FakeRoadmapper : IRoadmapper
{
    /// <summary>
    /// The canned roadmap result to return. Configure this before testing.
    /// </summary>
    public RoadmapResult? CannedResult { get; set; }

    /// <summary>
    /// Gets the last roadmap context that was passed to GenerateRoadmapAsync.
    /// Useful for test assertions.
    /// </summary>
    public RoadmapContext? LastContext { get; private set; }

    /// <summary>
    /// Generates a roadmap and returns a canned result.
    /// </summary>
    public Task<RoadmapResult> GenerateRoadmapAsync(RoadmapContext context, CancellationToken ct = default)
    {
        LastContext = context;

        if (CannedResult != null)
        {
            return Task.FromResult(CannedResult);
        }

        // Return default success result
        var now = DateTimeOffset.UtcNow;
        return Task.FromResult(new RoadmapResult
        {
            IsSuccess = true,
            RoadmapId = $"roadmap-{context.RunId}",
            RoadmapSpecPath = $"{context.WorkspacePath}/.aos/spec/roadmap.json",
            MilestoneSpecs = new List<MilestoneSpec>
            {
                new()
                {
                    MilestoneId = "MS-0001",
                    Name = "Initial Setup",
                    Description = "Set up the project foundation",
                    SpecPath = $"{context.WorkspacePath}/.aos/spec/milestones/MS-0001.json",
                    PhaseIds = new List<string> { "PH-0001" }
                }
            },
            PhaseSpecs = new List<PhaseSpec>
            {
                new()
                {
                    PhaseId = "PH-0001",
                    Name = "Phase 1",
                    Description = "Initial project phase",
                    SpecPath = $"{context.WorkspacePath}/.aos/spec/phases/PH-0001.json",
                    MilestoneId = "MS-0001",
                    Status = "pending",
                    SequenceOrder = 1
                }
            },
            StartedAt = now,
            CompletedAt = now.AddSeconds(1)
        });
    }
}
