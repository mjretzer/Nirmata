namespace Gmsd.Aos.Engine.Workspace;

internal sealed record AosWorkspaceRepairResult(
    string AosRootPath,
    AosWorkspaceRepairOutcome Outcome,
    IReadOnlyList<string> SchemaValidationIssues,
    AosWorkspaceComplianceReport? ComplianceReport,
    TimeSpan? Duration = null)
{
    public static AosWorkspaceRepairResult Success(string aosRootPath, IReadOnlyList<string> schemaValidationIssues, TimeSpan? duration = null) =>
        new(aosRootPath, AosWorkspaceRepairOutcome.Success, schemaValidationIssues, null, duration);

    public static AosWorkspaceRepairResult FailedToAcquireLock(string aosRootPath, string message) =>
        new(aosRootPath, AosWorkspaceRepairOutcome.FailedToAcquireLock, [], null);

    public static AosWorkspaceRepairResult FailedComplianceCheck(string aosRootPath, AosWorkspaceComplianceReport report) =>
        new(aosRootPath, AosWorkspaceRepairOutcome.FailedComplianceCheck, [], report);
}

internal enum AosWorkspaceRepairOutcome
{
    Success = 1,
    FailedToAcquireLock = 2,
    FailedComplianceCheck = 3
}
