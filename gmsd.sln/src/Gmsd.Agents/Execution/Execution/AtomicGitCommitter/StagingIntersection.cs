using System.Text;
using System.Text.RegularExpressions;

namespace Gmsd.Agents.Execution.Execution.AtomicGitCommitter;

/// <summary>
/// Computes the intersection of changed files and allowed file scopes.
/// Files must be both changed AND match at least one scope pattern to be included.
/// </summary>
public static class StagingIntersection
{
    /// <summary>
    /// Computes the ordered intersection of changed files and allowed scopes.
    /// </summary>
    /// <param name="changedFiles">The list of files that have been changed.</param>
    /// <param name="fileScopes">The list of allowed file scope patterns (supports glob patterns like "src/**/*.cs").</param>
    /// <returns>The ordered list of files to stage, sorted alphabetically for deterministic ordering.</returns>
    public static IReadOnlyList<string> Compute(IReadOnlyList<string> changedFiles, IReadOnlyList<string> fileScopes)
    {
        if (changedFiles.Count == 0 || fileScopes.Count == 0)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();

        foreach (var file in changedFiles)
        {
            if (IsInScope(file, fileScopes))
            {
                result.Add(file);
            }
        }

        // Return alphabetically sorted for deterministic ordering
        return result.OrderBy(f => f, StringComparer.Ordinal).ToList();
    }

    /// <summary>
    /// Computes the intersection and provides detailed information about included and excluded files.
    /// </summary>
    /// <param name="changedFiles">The list of files that have been changed.</param>
    /// <param name="fileScopes">The list of allowed file scope patterns.</param>
    /// <returns>A result containing files to stage and excluded files with reasons.</returns>
    public static StagingIntersectionResult ComputeWithDetails(
        IReadOnlyList<string> changedFiles,
        IReadOnlyList<string> fileScopes)
    {
        if (changedFiles.Count == 0 || fileScopes.Count == 0)
        {
            return new StagingIntersectionResult(
                Array.Empty<string>(),
                changedFiles.Select(f => new ExcludedFile(f, "out of scope")).ToList());
        }

        var toStage = new List<string>();
        var excluded = new List<ExcludedFile>();

        foreach (var file in changedFiles)
        {
            if (IsInScope(file, fileScopes))
            {
                toStage.Add(file);
            }
            else
            {
                excluded.Add(new ExcludedFile(file, "out of scope"));
            }
        }

        // Return alphabetically sorted for deterministic ordering
        return new StagingIntersectionResult(
            toStage.OrderBy(f => f, StringComparer.Ordinal).ToList(),
            excluded.OrderBy(e => e.FilePath, StringComparer.Ordinal).ToList());
    }

    private static bool IsInScope(string file, IReadOnlyList<string> patterns)
    {
        var normalizedFile = file.Replace('\\', '/');
        var fileSegments = normalizedFile.Split('/');

        foreach (var pattern in patterns)
        {
            if (MatchesGlob(normalizedFile, fileSegments, pattern.Replace('\\', '/')))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchesGlob(string filePath, string[] fileSegments, string pattern)
    {
        var patternSegments = pattern.Split('/');
        return MatchesGlobSegments(fileSegments, patternSegments, 0, 0);
    }

    private static bool MatchesGlobSegments(string[] fileSegments, string[] patternSegments, int fileIdx, int patternIdx)
    {
        // Base case: both exhausted
        if (fileIdx >= fileSegments.Length && patternIdx >= patternSegments.Length)
        {
            return true;
        }

        // Base case: file exhausted but pattern has ** remaining
        if (fileIdx >= fileSegments.Length)
        {
            // Check if remaining pattern segments are all **
            for (int i = patternIdx; i < patternSegments.Length; i++)
            {
                if (patternSegments[i] != "**")
                    return false;
            }
            return true;
        }

        // Base case: pattern exhausted but file has more segments (and no ** to match them)
        if (patternIdx >= patternSegments.Length)
        {
            return false;
        }

        var patternSeg = patternSegments[patternIdx];

        // Handle ** - matches zero or more directory segments
        if (patternSeg == "**")
        {
            // Try matching zero segments (skip **)
            if (MatchesGlobSegments(fileSegments, patternSegments, fileIdx, patternIdx + 1))
            {
                return true;
            }

            // Try matching one or more segments
            for (int i = fileIdx + 1; i <= fileSegments.Length; i++)
            {
                if (MatchesGlobSegments(fileSegments, patternSegments, i, patternIdx + 1))
                {
                    return true;
                }
            }

            return false;
        }

        // For regular segments, the file and pattern must have matching remaining counts
        // unless the last pattern segment is ** which we handle above
        // If this is the last pattern segment but not the last file segment, no match
        if (patternIdx == patternSegments.Length - 1 && fileIdx < fileSegments.Length - 1)
        {
            return false;
        }

        // Handle single segment with * and ? wildcards
        if (!MatchesSegment(fileSegments[fileIdx], patternSeg))
        {
            return false;
        }

        // Continue to next segment
        return MatchesGlobSegments(fileSegments, patternSegments, fileIdx + 1, patternIdx + 1);
    }

    private static bool MatchesSegment(string fileSegment, string patternSegment)
    {
        int fileIdx = 0;
        int patternIdx = 0;

        while (fileIdx < fileSegment.Length && patternIdx < patternSegment.Length)
        {
            char patternChar = patternSegment[patternIdx];

            if (patternChar == '*')
            {
                // * matches any sequence of characters within this segment only
                // Try all possible match lengths
                for (int len = 0; len <= fileSegment.Length - fileIdx; len++)
                {
                    if (MatchesSegmentAt(fileSegment, fileIdx + len, patternSegment, patternIdx + 1))
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (patternChar == '?')
            {
                // ? matches exactly one character
                fileIdx++;
                patternIdx++;
            }
            else
            {
                // Literal character match (case-insensitive)
                if (char.ToLowerInvariant(fileSegment[fileIdx]) != char.ToLowerInvariant(patternChar))
                {
                    return false;
                }
                fileIdx++;
                patternIdx++;
            }
        }

        // Handle trailing * in pattern
        while (patternIdx < patternSegment.Length && patternSegment[patternIdx] == '*')
        {
            patternIdx++;
        }

        return fileIdx >= fileSegment.Length && patternIdx >= patternSegment.Length;
    }

    private static bool MatchesSegmentAt(string fileSegment, int fileIdx, string patternSegment, int patternIdx)
    {
        // Check if pattern remainder matches at this position
        if (patternIdx >= patternSegment.Length)
        {
            return fileIdx >= fileSegment.Length;
        }

        if (fileIdx > fileSegment.Length)
        {
            return false;
        }

        // Handle remaining pattern
        int fi = fileIdx;
        int pi = patternIdx;

        while (fi < fileSegment.Length && pi < patternSegment.Length)
        {
            char pc = patternSegment[pi];

            if (pc == '*')
            {
                // Another * - recurse
                for (int len = 0; len <= fileSegment.Length - fi; len++)
                {
                    if (MatchesSegmentAt(fileSegment, fi + len, patternSegment, pi + 1))
                    {
                        return true;
                    }
                }
                return false;
            }
            else if (pc == '?')
            {
                fi++;
                pi++;
            }
            else
            {
                if (char.ToLowerInvariant(fileSegment[fi]) != char.ToLowerInvariant(pc))
                {
                    return false;
                }
                fi++;
                pi++;
            }
        }

        // Handle trailing * in pattern
        while (pi < patternSegment.Length && patternSegment[pi] == '*')
        {
            pi++;
        }

        return fi >= fileSegment.Length && pi >= patternSegment.Length;
    }
}

/// <summary>
/// Represents the result of a staging intersection computation.
/// </summary>
public sealed class StagingIntersectionResult
{
    /// <summary>
    /// The files that should be staged (intersection of changed and in-scope).
    /// </summary>
    public IReadOnlyList<string> FilesToStage { get; }

    /// <summary>
    /// The files that were excluded with their exclusion reasons.
    /// </summary>
    public IReadOnlyList<ExcludedFile> ExcludedFiles { get; }

    public StagingIntersectionResult(IReadOnlyList<string> filesToStage, IReadOnlyList<ExcludedFile> excludedFiles)
    {
        FilesToStage = filesToStage;
        ExcludedFiles = excludedFiles;
    }
}

/// <summary>
/// Represents a file that was excluded from staging.
/// </summary>
public sealed class ExcludedFile
{
    /// <summary>
    /// The path of the excluded file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// The reason for exclusion.
    /// </summary>
    public string Reason { get; }

    public ExcludedFile(string filePath, string reason)
    {
        FilePath = filePath;
        Reason = reason;
    }
}
