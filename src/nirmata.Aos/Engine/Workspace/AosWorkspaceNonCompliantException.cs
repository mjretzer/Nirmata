using System.Text;

namespace nirmata.Aos.Engine.Workspace;

internal sealed class AosWorkspaceNonCompliantException : Exception
{
    public AosWorkspaceNonCompliantException(string aosRootPath, AosWorkspaceComplianceReport report)
        : base(BuildMessage(aosRootPath, report))
    {
        AosRootPath = aosRootPath;
        Report = report;
    }

    public string AosRootPath { get; }

    public AosWorkspaceComplianceReport Report { get; }

    private static string BuildMessage(string aosRootPath, AosWorkspaceComplianceReport report)
    {
        var sb = new StringBuilder();
        sb.Append("AOS workspace is non-compliant at ");
        sb.Append(aosRootPath);
        sb.Append(".\n");

        AppendList(sb, "Missing directories", report.MissingDirectories);
        AppendList(sb, "Invalid directories", report.InvalidDirectories);
        AppendList(sb, "Missing files", report.MissingFiles);
        AppendList(sb, "Invalid files", report.InvalidFiles);
        AppendList(sb, "Extra top-level entries", report.ExtraTopLevelEntries);

        sb.Append("Fix the issues above (or delete and re-run init).\n");
        return sb.ToString();
    }

    private static void AppendList(StringBuilder sb, string title, IReadOnlyList<string> items)
    {
        if (items.Count == 0) return;

        sb.Append("- ");
        sb.Append(title);
        sb.Append(":\n");

        foreach (var item in items)
        {
            sb.Append("  - ");
            sb.Append(item);
            sb.Append('\n');
        }
    }
}

