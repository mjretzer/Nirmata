using Gmsd.Agents.Models.Results;
using Gmsd.Agents.Models.Runtime;

namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Defines the contract for the Roadmapper workflow that transforms
/// a validated project specification into a structured execution roadmap.
/// </summary>
public interface IRoadmapper
{
    /// <summary>
    /// Generates a structured roadmap from a validated project specification.
    /// </summary>
    /// <param name="context">The execution context containing run information and project spec reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of roadmap generation including milestones, phases, and artifacts.</returns>
    Task<RoadmapResult> GenerateRoadmapAsync(RoadmapContext context, CancellationToken ct = default);
}
