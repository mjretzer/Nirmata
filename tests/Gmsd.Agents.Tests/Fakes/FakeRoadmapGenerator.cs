using Gmsd.Agents.Execution.Planning;
using Gmsd.Agents.Models.Contracts;

namespace Gmsd.Agents.Tests.Fakes;

/// <summary>
/// Fake implementation of IRoadmapGenerator for testing.
/// Returns default milestone/phase skeletons for testing purposes.
/// </summary>
public sealed class FakeRoadmapGenerator : IRoadmapGenerator
{
    /// <summary>
    /// The canned milestones to return. Configure this before testing.
    /// </summary>
    public List<MilestoneItem>? CannedMilestones { get; set; }

    /// <summary>
    /// Whether validation should pass. Defaults to true.
    /// </summary>
    public bool ValidationResult { get; set; } = true;

    /// <summary>
    /// Generates default milestones and phases for testing.
    /// </summary>
    public List<MilestoneItem> GenerateMilestones()
    {
        if (CannedMilestones != null)
        {
            return CannedMilestones;
        }

        // Return default milestone with phases
        return new List<MilestoneItem>
        {
            new()
            {
                MilestoneId = "MS-0001",
                Name = "Initial Delivery",
                Description = "The initial project delivery encompassing foundation, implementation, and validation phases.",
                SequenceOrder = 1,
                CompletionCriteria = new List<string>
                {
                    "Foundation phase completed",
                    "Implementation phase completed",
                    "Validation phase completed"
                },
                Phases = new List<PhaseItem>
                {
                    new()
                    {
                        PhaseId = "PH-0001",
                        Name = "Foundation",
                        Description = "Establish project foundation, setup infrastructure, and define core architecture.",
                        MilestoneId = "MS-0001",
                        SequenceOrder = 1,
                        Deliverables = new List<string> { "Project structure", "CI/CD pipeline", "Core dependencies" },
                        InputArtifacts = new List<string> { ".aos/spec/project.json" },
                        OutputArtifacts = new List<string> { ".aos/spec/phases/PH-0001/phase.json" }
                    },
                    new()
                    {
                        PhaseId = "PH-0002",
                        Name = "Implementation",
                        Description = "Implement core features and functionality based on project specification.",
                        MilestoneId = "MS-0001",
                        SequenceOrder = 2,
                        Deliverables = new List<string> { "Core features", "Integration components", "Documentation" },
                        InputArtifacts = new List<string> { ".aos/spec/phases/PH-0001/phase.json" },
                        OutputArtifacts = new List<string> { ".aos/spec/phases/PH-0002/phase.json" }
                    },
                    new()
                    {
                        PhaseId = "PH-0003",
                        Name = "Validation",
                        Description = "Validate implementation against specifications and acceptance criteria.",
                        MilestoneId = "MS-0001",
                        SequenceOrder = 3,
                        Deliverables = new List<string> { "Test results", "UAT sign-off", "Deployment artifacts" },
                        InputArtifacts = new List<string> { ".aos/spec/phases/PH-0002/phase.json" },
                        OutputArtifacts = new List<string> { ".aos/spec/phases/PH-0003/phase.json", ".aos/spec/uat/index.json" }
                    }
                }
            }
        };
    }

    /// <summary>
    /// Validates the roadmap against schema (fake implementation).
    /// </summary>
    public bool ValidateAgainstSchema(object roadmap, out List<string> errors)
    {
        errors = new List<string>();

        if (!ValidationResult)
        {
            errors.Add("Validation failed (fake).");
        }

        return ValidationResult;
    }
}
