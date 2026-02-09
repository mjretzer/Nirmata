using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Engine.Evidence;

internal static class AosEvidencePaths
{
    public static string GetRunsRootPath(string aosRootPath)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        return AosPathRouter.ToAosRootPath(aosRootPath, ".aos/evidence/runs/");
    }

    public static string GetRunRootPath(string aosRootPath, string runId)
    {
        return AosPathRouter.GetRunEvidenceRootPath(aosRootPath, runId);
    }
}

