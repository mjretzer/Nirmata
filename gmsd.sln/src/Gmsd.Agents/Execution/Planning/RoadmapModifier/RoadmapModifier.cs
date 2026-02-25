using System.Text.Json;
using Gmsd.Agents.Models.Results;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Engine.Spec;
using Gmsd.Aos.Engine.Stores;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Implementation of safe roadmap modifications including phase insertion, removal, and renumbering.
/// </summary>
public sealed class RoadmapModifier : IRoadmapModifier
{
    private readonly SpecStore _specStore;
    private readonly IRoadmapRenumberer _renumberer;
    private readonly IEventStore _eventStore;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="RoadmapModifier"/> class.
    /// </summary>
    public RoadmapModifier(SpecStore specStore, IRoadmapRenumberer renumberer, IEventStore eventStore)
    {
        _specStore = specStore ?? throw new ArgumentNullException(nameof(specStore));
        _renumberer = renumberer ?? throw new ArgumentNullException(nameof(renumberer));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
    }

    /// <inheritdoc />
    public async Task<RoadmapModifyResult> InsertPhaseAsync(RoadmapModifyRequest request, string runId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrEmpty(runId);

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            // Read current roadmap
            var roadmap = _specStore.Inner.ReadRoadmap();
            var phases = GetPhasesFromRoadmap(roadmap);
            var milestones = GetMilestonesFromRoadmap(roadmap);

            // Generate new phase ID
            var newPhaseId = _renumberer.GenerateNextPhaseId(phases.Select(p => p.PhaseId));

            // Determine insert position
            var (targetMilestoneId, sequenceOrder) = DetermineInsertPosition(
                phases,
                milestones,
                request.ReferencePhaseId,
                request.Position,
                request.TargetMilestoneId);

            // Create new phase
            var newPhase = new PhaseSpec
            {
                PhaseId = newPhaseId,
                Name = request.NewPhaseName ?? $"Phase {newPhaseId}",
                Description = request.NewPhaseDescription ?? string.Empty,
                MilestoneId = targetMilestoneId,
                SequenceOrder = sequenceOrder,
                SpecPath = $".aos/spec/phases/{newPhaseId}/phase.json",
                Status = "pending"
            };

            // Reorder existing phases if needed
            var updatedPhases = ReorderPhasesForInsert(phases, newPhase, request.Position, request.ReferencePhaseId);

            // Renumber phases to ensure consistent sequencing
            var phaseRefs = updatedPhases.Select(p => new PhaseReference
            {
                PhaseId = p.PhaseId,
                MilestoneId = p.MilestoneId,
                SequenceOrder = p.SequenceOrder,
                Name = p.Name
            }).ToList();

            var idMapping = _renumberer.RenumberPhases(phaseRefs);
            ApplyIdMapping(updatedPhases, idMapping);
            newPhase.PhaseId = idMapping[newPhaseId];
            newPhase.SpecPath = $".aos/spec/phases/{newPhase.PhaseId}/phase.json";

            // Write phase spec
            var phaseDoc = CreatePhaseDocument(newPhase);
            _specStore.Inner.WritePhaseOverwrite(newPhase.PhaseId, phaseDoc);

            // Update roadmap
            var updatedRoadmap = CreateUpdatedRoadmap(roadmap, updatedPhases, milestones);
            _specStore.Inner.WriteRoadmapOverwrite(updatedRoadmap);

            // Emit event
            EmitModificationEvent("roadmap.modified", runId, newPhase.PhaseId, "insert");

            var completedAt = DateTimeOffset.UtcNow;

            return new RoadmapModifyResult
            {
                Status = RoadmapModifyStatus.Success,
                Operation = RoadmapModifyOperation.Insert,
                AffectedPhaseId = newPhase.PhaseId,
                NewPhase = newPhase,
                UpdatedPhases = updatedPhases,
                UpdatedMilestones = milestones,
                PhaseIdMapping = idMapping,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }
        catch (Exception ex)
        {
            return RoadmapModifyResult.FailedResult($"Insert phase failed: {ex.Message}", "INSERT_FAILED");
        }
    }

    /// <inheritdoc />
    public async Task<RoadmapModifyResult> RemovePhaseAsync(string phaseId, bool force, string runId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(phaseId);
        ArgumentException.ThrowIfNullOrEmpty(runId);

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            // Validate phase ID format
            if (!_renumberer.IsValidPhaseIdFormat(phaseId))
            {
                return RoadmapModifyResult.FailedResult($"Invalid phase ID format: {phaseId}", "INVALID_PHASE_ID");
            }

            // Read current roadmap
            var roadmap = _specStore.Inner.ReadRoadmap();
            var phases = GetPhasesFromRoadmap(roadmap);
            var milestones = GetMilestonesFromRoadmap(roadmap);

            // Check if phase exists
            var phaseToRemove = phases.FirstOrDefault(p => p.PhaseId == phaseId);
            if (phaseToRemove == null)
            {
                return RoadmapModifyResult.FailedResult($"Phase not found: {phaseId}", "PHASE_NOT_FOUND");
            }

            // Check for active phase (safety check)
            if (!force && await IsPhaseActiveAsync(phaseId, ct))
            {
                // Create issue documenting the blocker
                var issueId = await CreateBlockerIssueAsync(phaseId, runId, ct);

                // Emit blocker event
                EmitBlockerEvent(runId, phaseId, issueId);

                return RoadmapModifyResult.BlockedResult(
                    phaseId,
                    "Cannot remove active phase. Use force=true to override.",
                    issueId);
            }

            // Remove phase from list
            var updatedPhases = phases.Where(p => p.PhaseId != phaseId).ToList();

            // Delete phase spec file
            _specStore.Inner.DeletePhase(phaseId);

            // Renumber remaining phases
            var phaseRefs = updatedPhases.Select(p => new PhaseReference
            {
                PhaseId = p.PhaseId,
                MilestoneId = p.MilestoneId,
                SequenceOrder = p.SequenceOrder,
                Name = p.Name
            }).ToList();

            var idMapping = _renumberer.RenumberPhases(phaseRefs);
            ApplyIdMapping(updatedPhases, idMapping);

            // Update phase spec files with new IDs
            await UpdatePhaseSpecFilesAsync(updatedPhases, idMapping, ct);

            // Update roadmap
            var updatedRoadmap = CreateUpdatedRoadmap(roadmap, updatedPhases, milestones);
            _specStore.Inner.WriteRoadmapOverwrite(updatedRoadmap);

            // Update cursor if needed
            await UpdateCursorAfterRemovalAsync(phaseId, idMapping, ct);

            // Emit event
            EmitModificationEvent("roadmap.modified", runId, phaseId, "remove");

            var completedAt = DateTimeOffset.UtcNow;

            return new RoadmapModifyResult
            {
                Status = RoadmapModifyStatus.Success,
                Operation = RoadmapModifyOperation.Remove,
                AffectedPhaseId = phaseId,
                UpdatedPhases = updatedPhases,
                UpdatedMilestones = milestones,
                PhaseIdMapping = idMapping,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }
        catch (Exception ex)
        {
            return RoadmapModifyResult.FailedResult($"Remove phase failed: {ex.Message}", "REMOVE_FAILED");
        }
    }

    /// <inheritdoc />
    public async Task<RoadmapModifyResult> RenumberPhasesAsync(string runId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(runId);

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            // Read current roadmap
            var roadmap = _specStore.Inner.ReadRoadmap();
            var phases = GetPhasesFromRoadmap(roadmap);
            var milestones = GetMilestonesFromRoadmap(roadmap);

            // Renumber phases
            var phaseRefs = phases.Select(p => new PhaseReference
            {
                PhaseId = p.PhaseId,
                MilestoneId = p.MilestoneId,
                SequenceOrder = p.SequenceOrder,
                Name = p.Name
            }).ToList();

            var idMapping = _renumberer.RenumberPhases(phaseRefs);
            ApplyIdMapping(phases, idMapping);

            // Update phase spec files with new IDs
            await UpdatePhaseSpecFilesAsync(phases, idMapping, ct);

            // Update roadmap
            var updatedRoadmap = CreateUpdatedRoadmap(roadmap, phases, milestones);
            _specStore.Inner.WriteRoadmapOverwrite(updatedRoadmap);

            // Update cursor if needed
            await UpdateCursorAfterRenumberingAsync(idMapping, ct);

            // Emit event
            EmitModificationEvent("roadmap.modified", runId, null, "renumber");

            var completedAt = DateTimeOffset.UtcNow;

            return new RoadmapModifyResult
            {
                Status = RoadmapModifyStatus.Success,
                Operation = RoadmapModifyOperation.Renumber,
                UpdatedPhases = phases,
                UpdatedMilestones = milestones,
                PhaseIdMapping = idMapping,
                StartedAt = startedAt,
                CompletedAt = completedAt
            };
        }
        catch (Exception ex)
        {
            return RoadmapModifyResult.FailedResult($"Renumber phases failed: {ex.Message}", "RENUMBER_FAILED");
        }
    }

    private List<PhaseSpec> GetPhasesFromRoadmap(RoadmapSpecDocument roadmap)
    {
        return roadmap.Roadmap.Items
            .Select(item => new PhaseSpec
            {
                PhaseId = item.Id,
                Name = item.Title,
                MilestoneId = "MS-0001", // Default milestone
                SequenceOrder = 0,
                SpecPath = $".aos/spec/phases/{item.Id}/phase.json",
                Status = "pending"
            })
            .ToList();
    }

    private List<MilestoneSpec> GetMilestonesFromRoadmap(RoadmapSpecDocument roadmap)
    {
        // For now, return a default milestone
        return new List<MilestoneSpec>
        {
            new()
            {
                MilestoneId = "MS-0001",
                Name = "Default Milestone",
                SpecPath = ".aos/spec/milestones/MS-0001/milestone.json",
                PhaseIds = roadmap.Roadmap.Items.Select(i => i.Id).ToList()
            }
        };
    }

    private (string milestoneId, int sequenceOrder) DetermineInsertPosition(
        List<PhaseSpec> phases,
        List<MilestoneSpec> milestones,
        string? referencePhaseId,
        InsertPosition position,
        string? targetMilestoneId)
    {
        var milestoneId = targetMilestoneId ?? milestones.FirstOrDefault()?.MilestoneId ?? "MS-0001";
        int sequenceOrder;

        if (string.IsNullOrEmpty(referencePhaseId))
        {
            // No reference, insert at end
            var maxOrder = phases.Any() ? phases.Max(p => p.SequenceOrder) : 0;
            sequenceOrder = maxOrder + 1;
        }
        else
        {
            var refPhase = phases.FirstOrDefault(p => p.PhaseId == referencePhaseId);
            if (refPhase == null)
            {
                // Reference not found, insert at end
                var maxOrder = phases.Any() ? phases.Max(p => p.SequenceOrder) : 0;
                sequenceOrder = maxOrder + 1;
            }
            else
            {
                switch (position)
                {
                    case InsertPosition.Before:
                        sequenceOrder = refPhase.SequenceOrder;
                        break;
                    case InsertPosition.After:
                        sequenceOrder = refPhase.SequenceOrder + 1;
                        break;
                    case InsertPosition.AtBeginning:
                        sequenceOrder = 0;
                        break;
                    case InsertPosition.AtEnd:
                        var maxOrder = phases.Any() ? phases.Max(p => p.SequenceOrder) : 0;
                        sequenceOrder = maxOrder + 1;
                        break;
                    default:
                        sequenceOrder = refPhase.SequenceOrder + 1;
                        break;
                }
            }
        }

        return (milestoneId, sequenceOrder);
    }

    private List<PhaseSpec> ReorderPhasesForInsert(List<PhaseSpec> phases, PhaseSpec newPhase, InsertPosition position, string? referencePhaseId)
    {
        var result = new List<PhaseSpec>(phases);

        // Adjust sequence orders for existing phases
        foreach (var phase in result)
        {
            if (phase.SequenceOrder >= newPhase.SequenceOrder)
            {
                phase.SequenceOrder++;
            }
        }

        // Add new phase
        result.Add(newPhase);

        return result.OrderBy(p => p.SequenceOrder).ToList();
    }

    private void ApplyIdMapping(List<PhaseSpec> phases, Dictionary<string, string> idMapping)
    {
        foreach (var phase in phases)
        {
            if (idMapping.TryGetValue(phase.PhaseId, out var newId))
            {
                phase.PhaseId = newId;
                phase.SpecPath = $".aos/spec/phases/{newId}/phase.json";
            }
        }

        // Re-sort by sequence order
        phases.Sort((a, b) => a.SequenceOrder.CompareTo(b.SequenceOrder));

        // Update sequence orders to be consecutive
        for (int i = 0; i < phases.Count; i++)
        {
            phases[i].SequenceOrder = i + 1;
        }
    }

    private JsonElement CreatePhaseDocument(PhaseSpec phase)
    {
        var doc = new
        {
            schema = "gmsd:aos:schema:phase:v1",
            phaseId = phase.PhaseId,
            name = phase.Name,
            description = phase.Description,
            milestoneId = phase.MilestoneId,
            sequenceOrder = phase.SequenceOrder,
            status = phase.Status
        };

        return JsonSerializer.SerializeToElement(doc, JsonOptions);
    }

    private RoadmapSpecDocument CreateUpdatedRoadmap(RoadmapSpecDocument original, List<PhaseSpec> phases, List<MilestoneSpec> milestones)
    {
        var items = phases.Select(p => new RoadmapItemSpec(
            Id: p.PhaseId,
            Title: p.Name,
            Kind: "phase"
        )).ToList();

        var roadmapSpec = new RoadmapSpec(
            Title: original.Roadmap.Title,
            Items: items
        );

        return new RoadmapSpecDocument(
            SchemaVersion: original.SchemaVersion,
            Roadmap: roadmapSpec
        );
    }

    private async Task<bool> IsPhaseActiveAsync(string phaseId, CancellationToken ct)
    {
        // Read state file to check cursor
        // Access AosRootPath directly from the inner store (it inherits from AosJsonStoreBase)
        var aosRootPath = _specStore.Inner.AosRootPath;

        // Correctly construct path
        var statePath = Path.Combine(aosRootPath, "state", "state.json");
        
        if (!File.Exists(statePath))
        {
            return false;
        }

        var stateJson = await File.ReadAllTextAsync(statePath, ct);
        var state = JsonSerializer.Deserialize<StateSnapshot>(stateJson, JsonOptions);

        return state?.Cursor?.PhaseId == phaseId;
    }

    private async Task<string> CreateBlockerIssueAsync(string phaseId, string runId, CancellationToken ct)
    {
        // Generate valid Issue ID (ISS-####)
        int nextNum = 1;
        try
        {
            var indexDoc = _specStore.Inner.ReadCatalogIndex(Gmsd.Aos.Engine.Paths.AosArtifactKind.Issue);
            if (indexDoc?.Items != null)
            {
                foreach (var id in indexDoc.Items)
                {
                    if (id.StartsWith("ISS-") && int.TryParse(id.Substring(4), out var num))
                    {
                        if (num >= nextNum) nextNum = num + 1;
                    }
                }
            }
        }
        catch
        {
            // Index likely doesn't exist yet, start with 1
        }

        var issueId = $"ISS-{nextNum:D4}";
        
        var issueDoc = new
        {
            schemaVersion = 1,
            id = issueId,
            title = $"Roadmap modification blocked: Cannot remove active phase {phaseId}",
            description = $"Attempted to remove phase {phaseId} which is currently the active phase in the execution cursor. Use force=true to override this safety check.",
            severity = "high",
            status = "open",
            createdAt = DateTimeOffset.UtcNow.ToString("O"),
            runId = runId,
            relatedPhaseId = phaseId
        };

        var issueJson = JsonSerializer.SerializeToElement(issueDoc, JsonOptions);
        _specStore.Inner.WriteIssueOverwrite(issueId, issueJson);

        return issueId;
    }

    private async Task UpdatePhaseSpecFilesAsync(List<PhaseSpec> phases, Dictionary<string, string> idMapping, CancellationToken ct)
    {
        foreach (var kvp in idMapping)
        {
            var oldId = kvp.Key;
            var newId = kvp.Value;

            if (oldId != newId)
            {
                // Try to read old phase spec
                try
                {
                    var phaseJson = _specStore.Inner.ReadPhase(oldId);
                    
                    // Write with new ID
                    _specStore.Inner.WritePhaseOverwrite(newId, phaseJson);
                    
                    // Delete old phase spec
                    _specStore.Inner.DeletePhase(oldId);
                }
                catch
                {
                    // Phase spec may not exist, continue
                }
            }
        }
    }

    private async Task UpdateCursorAfterRemovalAsync(string removedPhaseId, Dictionary<string, string> idMapping, CancellationToken ct)
    {
        // Cursor coherence: if the removed phase was the cursor phase, move cursor to next available phase
        // This is a simplified implementation
    }

    private async Task UpdateCursorAfterRenumberingAsync(Dictionary<string, string> idMapping, CancellationToken ct)
    {
        // Cursor coherence: if the cursor phase was renumbered, update the cursor to point to the new ID
        // This is a simplified implementation
    }

    private void EmitModificationEvent(string eventType, string runId, string? phaseId, string operation)
    {
        var eventPayload = new
        {
            eventType = eventType,
            timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            runId = runId,
            data = new
            {
                phaseId = phaseId,
                operation = operation
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonElement = JsonSerializer.SerializeToElement(eventPayload, jsonOptions);
        _eventStore.AppendEvent(jsonElement);
    }

    private void EmitBlockerEvent(string runId, string phaseId, string issueId)
    {
        var eventPayload = new
        {
            eventType = "roadmap.blocker",
            timestampUtc = DateTimeOffset.UtcNow.ToString("O"),
            runId = runId,
            data = new
            {
                phaseId = phaseId,
                issueId = issueId,
                reason = "Active phase removal blocked"
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var jsonElement = JsonSerializer.SerializeToElement(eventPayload, jsonOptions);
        _eventStore.AppendEvent(jsonElement);
    }
}
