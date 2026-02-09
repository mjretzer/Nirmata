using System.Text.RegularExpressions;

namespace Gmsd.Agents.Execution.Planning.RoadmapModifier;

/// <summary>
/// Implementation of phase ID sequencing logic for consistent PH-#### format.
/// </summary>
public sealed class RoadmapRenumberer : IRoadmapRenumberer
{
    private const string PhaseIdPattern = @"^PH-(\d{4})$";
    private const string PhaseIdPrefix = "PH-";

    /// <inheritdoc />
    public string GenerateNextPhaseId(IEnumerable<string> existingPhaseIds)
    {
        var maxSequence = 0;

        foreach (var phaseId in existingPhaseIds)
        {
            var sequenceNumber = ExtractSequenceNumber(phaseId);
            if (sequenceNumber > maxSequence)
            {
                maxSequence = sequenceNumber;
            }
        }

        var nextSequence = maxSequence + 1;
        return $"{PhaseIdPrefix}{nextSequence:D4}";
    }

    /// <inheritdoc />
    public Dictionary<string, string> RenumberPhases(IEnumerable<PhaseReference> phases)
    {
        var mapping = new Dictionary<string, string>(StringComparer.Ordinal);
        var orderedPhases = phases.OrderBy(p => p.SequenceOrder).ToList();

        for (int i = 0; i < orderedPhases.Count; i++)
        {
            var oldPhaseId = orderedPhases[i].PhaseId;
            var newPhaseId = $"{PhaseIdPrefix}{i + 1:D4}";
            mapping[oldPhaseId] = newPhaseId;
        }

        return mapping;
    }

    /// <inheritdoc />
    public int ExtractSequenceNumber(string phaseId)
    {
        if (string.IsNullOrEmpty(phaseId))
        {
            return -1;
        }

        var match = Regex.Match(phaseId, PhaseIdPattern);
        if (match.Success && match.Groups.Count > 1)
        {
            if (int.TryParse(match.Groups[1].Value, out var sequenceNumber))
            {
                return sequenceNumber;
            }
        }

        return -1;
    }

    /// <inheritdoc />
    public bool IsValidPhaseIdFormat(string phaseId)
    {
        if (string.IsNullOrEmpty(phaseId))
        {
            return false;
        }

        return Regex.IsMatch(phaseId, PhaseIdPattern);
    }
}
