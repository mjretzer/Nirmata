namespace nirmata.Aos.Engine.Errors;

/// <summary>
/// Stable error codes for AOS failures.
/// </summary>
internal static class AosErrorCodes
{
    public const string CliInvalidUsage = "aos.cli.invalid_usage";

    public const string RepositoryRootNotFound = "aos.workspace.repository_root_not_found";

    public const string PolicyViolation = "aos.policy.violation";

    public const string UnexpectedInternalError = "aos.unexpected_internal_error";

    public const string InvalidJson = "aos.json.invalid";

    public const string SchemaPackInvalid = "aos.schemas.invalid_pack";

    public const string WorkspaceNonCompliant = "aos.workspace.non_compliant";
    public const string WorkspaceValidationFailed = "aos.workspace.validation_failed";

    public const string LockContended = "aos.lock.contended";
    public const string LockInvalid = "aos.lock.invalid";

    public const string ConfigInvalid = "aos.config.invalid";

    public const string ExecutePlanInvalidPlan = "aos.execute_plan.invalid_plan";

    public const string RepairIndexesFailed = "aos.repair.indexes_failed";

    public const string InvalidStateTransition = "aos.state.invalid_transition";

    public const string CheckpointFailed = "aos.checkpoint.failed";
}

