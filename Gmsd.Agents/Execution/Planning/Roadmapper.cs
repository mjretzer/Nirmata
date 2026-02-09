using System.Text.Json;
using Gmsd.Agents.Models.Contracts;
using Gmsd.Agents.Models.Results;
using Gmsd.Agents.Models.Runtime;
using Gmsd.Aos.Engine.Spec;
using Gmsd.Aos.Engine.Stores;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Execution.Planning;

/// <summary>
/// Default implementation of the IRoadmapper interface.
/// Generates roadmap specifications, persists artifacts, and manages state.
/// </summary>
public sealed class Roadmapper : IRoadmapper
{
    private readonly IRoadmapGenerator _generator;
    private readonly SpecStore _specStore;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="Roadmapper"/> class.
    /// </summary>
    public Roadmapper(IRoadmapGenerator generator, SpecStore specStore)
    {
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _specStore = specStore ?? throw new ArgumentNullException(nameof(specStore));
    }

    /// <inheritdoc />
    public async Task<RoadmapResult> GenerateRoadmapAsync(RoadmapContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;
        var roadmapId = $"RDMP-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8]}";

        try
        {
            // Generate milestone and phase skeletons
            var milestones = _generator.GenerateMilestones();

            // Build spec artifacts
            var milestoneSpecs = new List<MilestoneSpec>();
            var phaseSpecs = new List<PhaseSpec>();

            foreach (var milestone in milestones)
            {
                var msSpec = new MilestoneSpec
                {
                    MilestoneId = milestone.MilestoneId,
                    Name = milestone.Name,
                    Description = milestone.Description,
                    SpecPath = $".aos/spec/milestones/{milestone.MilestoneId}/milestone.json",
                    PhaseIds = milestone.Phases.Select(p => p.PhaseId).ToList()
                };
                milestoneSpecs.Add(msSpec);

                // Write milestone spec
                var milestoneDoc = new
                {
                    schema = "gmsd:aos:schema:milestone:v1",
                    milestoneId = milestone.MilestoneId,
                    name = milestone.Name,
                    description = milestone.Description,
                    completionCriteria = milestone.CompletionCriteria,
                    phaseIds = milestone.Phases.Select(p => p.PhaseId).ToList()
                };

                var milestoneJson = JsonSerializer.SerializeToElement(milestoneDoc, JsonOptions);
                _specStore.Inner.WriteMilestoneOverwrite(milestone.MilestoneId, milestoneJson);

                // Process phases
                foreach (var phase in milestone.Phases)
                {
                    var phaseSpec = new PhaseSpec
                    {
                        PhaseId = phase.PhaseId,
                        Name = phase.Name,
                        Description = phase.Description,
                        MilestoneId = milestone.MilestoneId,
                        SequenceOrder = phase.SequenceOrder,
                        SpecPath = $".aos/spec/phases/{phase.PhaseId}/phase.json",
                        Status = "pending"
                    };
                    phaseSpecs.Add(phaseSpec);

                    // Write phase spec
                    var phaseDoc = new
                    {
                        schema = "gmsd:aos:schema:phase:v1",
                        phaseId = phase.PhaseId,
                        name = phase.Name,
                        description = phase.Description,
                        milestoneId = milestone.MilestoneId,
                        sequenceOrder = phase.SequenceOrder,
                        status = "pending",
                        deliverables = phase.Deliverables,
                        inputArtifacts = phase.InputArtifacts,
                        outputArtifacts = phase.OutputArtifacts
                    };

                    var phaseJson = JsonSerializer.SerializeToElement(phaseDoc, JsonOptions);
                    _specStore.Inner.WritePhaseOverwrite(phase.PhaseId, phaseJson);
                }
            }

            // Build and write roadmap spec
            var roadmapDoc = new
            {
                schema = "gmsd:aos:schema:roadmap:v1",
                roadmapId,
                projectId = context.ProjectSpec.ProjectId,
                milestones = milestoneSpecs.Select(m => new
                {
                    milestoneId = m.MilestoneId,
                    name = m.Name,
                    phaseIds = m.PhaseIds
                }).ToList(),
                phases = phaseSpecs.Select(p => new
                {
                    phaseId = p.PhaseId,
                    name = p.Name,
                    milestoneId = p.MilestoneId,
                    sequenceOrder = p.SequenceOrder
                }).ToList()
            };

            // Validate roadmap against schema
            if (!_generator.ValidateAgainstSchema(roadmapDoc, out var validationErrors))
            {
                return new RoadmapResult
                {
                    IsSuccess = false,
                    RoadmapId = roadmapId,
                    Error = $"Roadmap validation failed: {string.Join(", ", validationErrors)}",
                    ErrorCode = "VALIDATION_FAILED",
                    StartedAt = startedAt,
                    CompletedAt = DateTimeOffset.UtcNow
                };
            }

            // Write roadmap spec using the correct document type
            var roadmapSpec = new RoadmapSpec(
                Title: "Initial Delivery Roadmap",
                Items: phaseSpecs.Select(p => new RoadmapItemSpec(
                    Id: p.PhaseId,
                    Title: p.Name,
                    Kind: "phase"
                )).ToList()
            );

            var roadmapDocRecord = new RoadmapSpecDocument(
                SchemaVersion: 1,
                Roadmap: roadmapSpec
            );

            _specStore.Inner.WriteRoadmapOverwrite(roadmapDocRecord);

            // Write state with cursor at first phase
            var firstPhaseId = phaseSpecs.OrderBy(p => p.SequenceOrder).FirstOrDefault()?.PhaseId ?? "PH-0001";
            var stateDoc = new
            {
                schema = "gmsd:aos:schema:state:v1",
                cursor = new
                {
                    phaseId = firstPhaseId,
                    phaseStatus = "pending",
                    taskId = (string?)null,
                    taskStatus = (string?)null,
                    stepId = (string?)null,
                    stepStatus = (string?)null
                },
                metadata = new Dictionary<string, object>
                {
                    ["roadmapId"] = roadmapId,
                    ["createdAt"] = DateTimeOffset.UtcNow.ToString("O")
                }
            };

            // Write state file
            var statePath = Path.Combine(context.WorkspacePath, ".aos/state/state.json");
            Directory.CreateDirectory(Path.GetDirectoryName(statePath)!);
            await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(stateDoc, JsonOptions), ct);

            // Append event to events.ndjson
            var eventEntry = new
            {
                eventType = "roadmap.created",
                timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
                runId = context.RunId,
                correlationId = context.CorrelationId,
                data = new
                {
                    roadmapId,
                    milestoneCount = milestoneSpecs.Count,
                    phaseCount = phaseSpecs.Count,
                    firstPhaseId
                }
            };

            var eventsPath = Path.Combine(context.WorkspacePath, ".aos/state/events.ndjson");
            Directory.CreateDirectory(Path.GetDirectoryName(eventsPath)!);
            var eventLine = JsonSerializer.Serialize(eventEntry, JsonOptions);
            await File.AppendAllTextAsync(eventsPath, eventLine + "\n", ct);

            var completedAt = DateTimeOffset.UtcNow;

            return new RoadmapResult
            {
                IsSuccess = true,
                RoadmapId = roadmapId,
                RoadmapSpecPath = ".aos/spec/roadmap.json",
                MilestoneSpecs = milestoneSpecs,
                PhaseSpecs = phaseSpecs,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }
        catch (Exception ex)
        {
            return new RoadmapResult
            {
                IsSuccess = false,
                RoadmapId = roadmapId,
                Error = $"Roadmap generation failed: {ex.Message}",
                ErrorCode = "GENERATION_FAILED",
                StartedAt = startedAt,
                CompletedAt = DateTimeOffset.UtcNow
            };
        }
    }
}
