using System.Text;

namespace nirmata.Agents.Execution.Evidence;

/// <summary>
/// Generates unified diffs (RFC 3881) comparing initial and final file states.
/// Ensures deterministic output with consistent line endings and no timestamps.
/// </summary>
public sealed class DiffGenerator
{
    /// <summary>
    /// Generates a unified diff between two file states.
    /// </summary>
    /// <param name="originalPath">Path to the original file (or baseline).</param>
    /// name="modifiedPath">Path to the modified file.</param>
    /// <returns>Unified diff format string.</returns>
    public string GenerateDiff(string originalPath, string modifiedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(modifiedPath);

        try
        {
            var originalContent = File.Exists(originalPath) ? File.ReadAllLines(originalPath) : Array.Empty<string>();
            var modifiedContent = File.Exists(modifiedPath) ? File.ReadAllLines(modifiedPath) : Array.Empty<string>();

            return GenerateDiffFromLines(originalPath, modifiedPath, originalContent, modifiedContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to generate diff for {originalPath}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates a unified diff from two sets of lines.
    /// </summary>
    public string GenerateDiffFromLines(string originalName, string modifiedName, string[] originalLines, string[] modifiedLines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(modifiedName);
        ArgumentNullException.ThrowIfNull(originalLines);
        ArgumentNullException.ThrowIfNull(modifiedLines);

        var sb = new StringBuilder();

        // Write unified diff header
        sb.AppendLine($"--- {originalName}");
        sb.AppendLine($"+++ {modifiedName}");

        // Compute diff using simple line-based algorithm
        var hunks = ComputeHunks(originalLines, modifiedLines);

        foreach (var hunk in hunks)
        {
            sb.AppendLine(hunk);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Computes diff hunks using a simple line-based algorithm.
    /// </summary>
    private static List<string> ComputeHunks(string[] originalLines, string[] modifiedLines)
    {
        var hunks = new List<string>();
        var lcs = ComputeLongestCommonSubsequence(originalLines, modifiedLines);

        var origIdx = 0;
        var modIdx = 0;
        var hunkStart = 0;
        var hunkLines = new List<string>();

        while (origIdx < originalLines.Length || modIdx < modifiedLines.Length)
        {
            if (origIdx < originalLines.Length && modIdx < modifiedLines.Length &&
                originalLines[origIdx] == modifiedLines[modIdx])
            {
                // Lines match
                if (hunkLines.Count > 0)
                {
                    hunks.Add(FormatHunk(hunkStart, origIdx, hunkLines));
                    hunkLines.Clear();
                }
                origIdx++;
                modIdx++;
                hunkStart = origIdx;
            }
            else if (origIdx < originalLines.Length)
            {
                // Line removed
                hunkLines.Add($"-{originalLines[origIdx]}");
                origIdx++;
            }
            else if (modIdx < modifiedLines.Length)
            {
                // Line added
                hunkLines.Add($"+{modifiedLines[modIdx]}");
                modIdx++;
            }
        }

        if (hunkLines.Count > 0)
        {
            hunks.Add(FormatHunk(hunkStart, origIdx, hunkLines));
        }

        return hunks;
    }

    /// <summary>
    /// Computes the longest common subsequence (simplified for determinism).
    /// </summary>
    private static List<int> ComputeLongestCommonSubsequence(string[] original, string[] modified)
    {
        // Simplified LCS for deterministic output
        var lcs = new List<int>();
        var origIdx = 0;
        var modIdx = 0;

        while (origIdx < original.Length && modIdx < modified.Length)
        {
            if (original[origIdx] == modified[modIdx])
            {
                lcs.Add(origIdx);
                origIdx++;
                modIdx++;
            }
            else
            {
                origIdx++;
            }
        }

        return lcs;
    }

    /// <summary>
    /// Formats a diff hunk in unified diff format.
    /// </summary>
    private static string FormatHunk(int startLine, int endLine, List<string> changes)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"@@ -{startLine + 1},{endLine - startLine} +{startLine + 1},{changes.Count} @@");

        foreach (var change in changes)
        {
            sb.AppendLine(change);
        }

        return sb.ToString();
    }
}
