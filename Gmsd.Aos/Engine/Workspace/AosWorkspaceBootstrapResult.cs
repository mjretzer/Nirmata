namespace Gmsd.Aos.Engine.Workspace;

internal sealed record AosWorkspaceBootstrapResult(
    string AosRootPath,
    AosWorkspaceBootstrapOutcome Outcome)
{
    public static AosWorkspaceBootstrapResult Created(string aosRootPath) =>
        new(aosRootPath, AosWorkspaceBootstrapOutcome.Created);

    public static AosWorkspaceBootstrapResult NoChanges(string aosRootPath) =>
        new(aosRootPath, AosWorkspaceBootstrapOutcome.NoChanges);
}

internal enum AosWorkspaceBootstrapOutcome
{
    Created = 1,
    NoChanges = 2
}

