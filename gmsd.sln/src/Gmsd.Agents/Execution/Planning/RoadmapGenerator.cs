using System.Text.Json;
using Gmsd.Agents.Models.Contracts;

namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Generates deterministic milestone and phase skeletons for roadmap creation.
/// </summary>
public interface IRoadmapGenerator
{
    /// <summary>
    /// Generates default milestones and phases for a project roadmap.
    /// </summary>
    /// <returns>A collection of milestone items containing their associated phases.</returns>
    List<MilestoneItem> GenerateMilestones();

    /// <summary>
    /// Validates the generated roadmap against the schema.
    /// </summary>
    /// <param name="roadmap">The roadmap data to validate.</param>
    /// <returns>True if valid; otherwise false with error details.</returns>
    bool ValidateAgainstSchema(object roadmap, out List<string> errors);
}

/// <summary>
/// Default implementation of the roadmap generator.
/// </summary>
public sealed class RoadmapGenerator : IRoadmapGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <inheritdoc />
    public List<MilestoneItem> GenerateMilestones()
    {
        var milestones = new List<MilestoneItem>();

        // Generate default milestone MS-0001: Initial Delivery
        var milestone = new MilestoneItem
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
            }
        };

        // Generate default phases for this milestone
        var phases = new List<PhaseItem>
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
        };

        milestone.Phases = phases;
        milestones.Add(milestone);

        return milestones;
    }

    /// <inheritdoc />
    public bool ValidateAgainstSchema(object roadmap, out List<string> errors)
    {
        errors = new List<string>();

        try
        {
            var json = JsonSerializer.Serialize(roadmap, JsonOptions);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check required schema identifier
            if (!root.TryGetProperty("schema", out var schemaElement) &&
                !root.TryGetProperty("schemaVersion", out schemaElement))
            {
                errors.Add("Missing required 'schema' or 'schemaVersion' property.");
                return false;
            }

            var schemaValue = schemaElement.GetString();
            if (schemaValue != "gmsd:aos:schema:roadmap:v1" && schemaValue != "1")
            {
                errors.Add($"Unexpected schema: {schemaValue}. Expected: gmsd:aos:schema:roadmap:v1");
            }

            // Check milestones presence
            if (!root.TryGetProperty("milestones", out var milestonesElement) || milestonesElement.GetArrayLength() == 0)
            {
                errors.Add("Roadmap must contain at least one milestone.");
            }

            // Check phases presence
            if (!root.TryGetProperty("phases", out var phasesElement) || phasesElement.GetArrayLength() == 0)
            {
                errors.Add("Roadmap must contain at least one phase.");
            }

            return errors.Count == 0;
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return false;
        }
    }
}
