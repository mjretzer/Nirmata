namespace Gmsd.Aos.Engine.Workspace;

internal sealed record AosWorkspaceComplianceReport(
    string AosRootPath,
    bool IsCompliant,
    bool AosRootExists,
    IReadOnlyList<string> MissingDirectories,
    IReadOnlyList<string> InvalidDirectories,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> InvalidFiles,
    IReadOnlyList<string> ExtraTopLevelEntries)
{
    public static AosWorkspaceComplianceReport CompliantDoesNotExist(string aosRootPath) =>
        new(
            aosRootPath,
            IsCompliant: true,
            AosRootExists: false,
            MissingDirectories: [],
            InvalidDirectories: [],
            MissingFiles: [],
            InvalidFiles: [],
            ExtraTopLevelEntries: []
        );

    public static AosWorkspaceComplianceReport NonCompliantMissingAosRoot(string aosRootPath) =>
        new(
            aosRootPath,
            IsCompliant: false,
            AosRootExists: false,
            MissingDirectories: [".aos/"],
            InvalidDirectories: [],
            MissingFiles: [],
            InvalidFiles: [],
            ExtraTopLevelEntries: []
        );

    public static AosWorkspaceComplianceReport NonCompliantAosRootIsFile(string aosRootPath) =>
        new(
            aosRootPath,
            IsCompliant: false,
            AosRootExists: false,
            MissingDirectories: [],
            InvalidDirectories: [".aos/ (expected directory, found file)"],
            MissingFiles: [],
            InvalidFiles: [],
            ExtraTopLevelEntries: []
        );

    public static AosWorkspaceComplianceReport FromChecks(
        string aosRootPath,
        IReadOnlyList<string> missingDirectories,
        IReadOnlyList<string> invalidDirectories,
        IReadOnlyList<string> missingFiles,
        IReadOnlyList<string> invalidFiles,
        IReadOnlyList<string> extraTopLevelEntries)
    {
        var isCompliant =
            missingDirectories.Count == 0 &&
            invalidDirectories.Count == 0 &&
            missingFiles.Count == 0 &&
            invalidFiles.Count == 0 &&
            extraTopLevelEntries.Count == 0;

        return new AosWorkspaceComplianceReport(
            aosRootPath,
            IsCompliant: isCompliant,
            AosRootExists: true,
            MissingDirectories: missingDirectories,
            InvalidDirectories: invalidDirectories,
            MissingFiles: missingFiles,
            InvalidFiles: invalidFiles,
            ExtraTopLevelEntries: extraTopLevelEntries
        );
    }
}

