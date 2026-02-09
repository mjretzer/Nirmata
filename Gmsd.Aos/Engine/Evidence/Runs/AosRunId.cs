using System.Text.RegularExpressions;

namespace Gmsd.Aos.Engine.Evidence;

internal static class AosRunId
{
    // Folder-safe, opaque, dependency-free: GUID (N) lower-case hex.
    public static string New() => Guid.NewGuid().ToString("N").ToLowerInvariant();

    // Accept only what we generate today (32 lower-case hex chars).
    // This can be relaxed later if we add alternative run id formats.
    private static readonly Regex ValidRunId = new("^[0-9a-f]{32}$", RegexOptions.Compiled);

    public static bool IsValid(string? runId) =>
        !string.IsNullOrWhiteSpace(runId) && ValidRunId.IsMatch(runId);
}

