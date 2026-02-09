using System.Linq;
using System.Text;

namespace Gmsd.Aos.Engine.ExecutePlan;

internal static class ExecutePlanExecutor
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes all plan outputs under the provided run outputs root path.
    /// </summary>
    /// <remarks>
    /// Output files are written deterministically for identical plans by ordering outputs using ordinal
    /// string ordering over <see cref="ExecutePlanPlanOutput.RelativePath"/>, then by input index.
    /// </remarks>
    public static IReadOnlyList<string> WriteOutputs(string runOutputsRootPath, ExecutePlanPlan plan)
    {
        if (string.IsNullOrWhiteSpace(runOutputsRootPath))
        {
            throw new ArgumentException("Missing run outputs root path.", nameof(runOutputsRootPath));
        }

        if (plan is null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        Directory.CreateDirectory(runOutputsRootPath);

        var ordered = plan.Outputs
            .Select((o, idx) => (Output: o, Index: idx))
            .OrderBy(x => x.Output.RelativePath, StringComparer.Ordinal)
            .ThenBy(x => x.Index)
            .ToArray();

        foreach (var item in ordered)
        {
            var fullPath = ExecutePlanOutputPathPolicy.ResolveFilePathUnderRoot(
                runOutputsRootPath,
                item.Output.RelativePath
            );

            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            WriteFileNoBomUtf8Deterministically(fullPath, item.Output.ContentsUtf8);
        }

        return ordered.Select(x => x.Output.RelativePath).ToArray();
    }

    private static void WriteFileNoBomUtf8Deterministically(string path, string contentsUtf8)
    {
        // Avoid churn if the contents are already identical.
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path, Utf8NoBom);
            if (string.Equals(existing, contentsUtf8, StringComparison.Ordinal))
            {
                return;
            }
        }

        File.WriteAllText(path, contentsUtf8, Utf8NoBom);
    }
}

