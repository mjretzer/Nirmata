using System.Text.Json;
using Gmsd.Agents.Models.Results;
using Gmsd.Aos.Engine.Spec;

namespace Gmsd.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Validates roadmap integrity after modifications.
/// </summary>
public sealed class RoadmapValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Validates that the modified roadmap is consistent and complete.
    /// </summary>
    /// <param name="phases">The phases in the roadmap.</param>
    /// <param name="milestones">The milestones in the roadmap.</param>
    /// <returns>A tuple of (isValid, validationErrors).</returns>
    public (bool isValid, List<string> errors) ValidateRoadmap(List<PhaseSpec> phases, List<MilestoneSpec> milestones)
    {
        var errors = new List<string>();

        // Validate phase ID format
        foreach (var phase in phases)
        {
            if (!IsValidPhaseIdFormat(phase.PhaseId))
            {
                errors.Add($"Invalid phase ID format: {phase.PhaseId}");
            }
        }

        // Validate unique phase IDs
        var duplicatePhaseIds = phases
            .GroupBy(p => p.PhaseId)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicatePhaseIds)
        {
            errors.Add($"Duplicate phase ID: {dup}");
        }

        // Validate unique sequence orders
        var duplicateSequences = phases
            .GroupBy(p => p.SequenceOrder)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicateSequences)
        {
            errors.Add($"Duplicate sequence order: {dup}");
        }

        // Validate sequence order is consecutive starting from 1
        var orderedPhases = phases.OrderBy(p => p.SequenceOrder).ToList();
        for (int i = 0; i < orderedPhases.Count; i++)
        {
            var expectedOrder = i + 1;
            if (orderedPhases[i].SequenceOrder != expectedOrder)
            {
                errors.Add($"Sequence order gap: expected {expectedOrder} but found {orderedPhases[i].SequenceOrder}");
            }
        }

        // Validate milestone references
        var validMilestoneIds = milestones.Select(m => m.MilestoneId).ToHashSet();
        foreach (var phase in phases)
        {
            if (!validMilestoneIds.Contains(phase.MilestoneId))
            {
                errors.Add($"Phase {phase.PhaseId} references invalid milestone: {phase.MilestoneId}");
            }
        }

        // Validate phase references in milestones
        var validPhaseIds = phases.Select(p => p.PhaseId).ToHashSet();
        foreach (var milestone in milestones)
        {
            foreach (var phaseId in milestone.PhaseIds)
            {
                if (!validPhaseIds.Contains(phaseId))
                {
                    errors.Add($"Milestone {milestone.MilestoneId} references invalid phase: {phaseId}");
                }
            }
        }

        // Validate no orphaned phases
        var referencedPhaseIds = milestones.SelectMany(m => m.PhaseIds).ToHashSet();
        foreach (var phase in phases)
        {
            if (!referencedPhaseIds.Contains(phase.PhaseId))
            {
                errors.Add($"Orphaned phase not referenced by any milestone: {phase.PhaseId}");
            }
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// Validates a roadmap spec document against the schema.
    /// </summary>
    internal (bool isValid, List<string> errors) ValidateRoadmapSpec(RoadmapSpecDocument roadmap)
    {
        var errors = new List<string>();

        if (roadmap.SchemaVersion != 1)
        {
            errors.Add($"Unsupported schema version: {roadmap.SchemaVersion}");
        }

        if (roadmap.Roadmap.Items.Count == 0)
        {
            errors.Add("Roadmap contains no phases");
        }

        // Validate all items have required fields
        for (int i = 0; i < roadmap.Roadmap.Items.Count; i++)
        {
            var item = roadmap.Roadmap.Items[i];
            if (string.IsNullOrEmpty(item.Id))
            {
                errors.Add($"Item at index {i} has no ID");
            }
            if (string.IsNullOrEmpty(item.Title))
            {
                errors.Add($"Item at index {i} has no title");
            }
        }

        return (errors.Count == 0, errors);
    }

    private static bool IsValidPhaseIdFormat(string phaseId)
    {
        if (string.IsNullOrEmpty(phaseId))
        {
            return false;
        }

        // PH-#### format
        if (!phaseId.StartsWith("PH-", StringComparison.Ordinal))
        {
            return false;
        }

        var numberPart = phaseId[3..];
        if (numberPart.Length != 4)
        {
            return false;
        }

        return numberPart.All(char.IsDigit);
    }
}
