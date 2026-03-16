using System.Text.Json;
using nirmata.Aos.Engine.ExecutePlan;
using nirmata.Aos.Engine.Repair;
using nirmata.Aos.Engine.StateTransitions;
using nirmata.Aos.Engine.Validation;
using nirmata.Aos.Engine.Workspace;

namespace nirmata.Aos.Engine.Errors;

internal static class AosErrorMapper
{
    public static AosErrorEnvelope Map(Exception ex)
    {
        if (ex is null) throw new ArgumentNullException(nameof(ex));

        return ex switch
        {
            AosPolicyViolationException p => new AosErrorEnvelope(
                Code: AosErrorCodes.PolicyViolation,
                Message: p.Message
            ),

            AosRepositoryRootNotFoundException r => new AosErrorEnvelope(
                Code: AosErrorCodes.RepositoryRootNotFound,
                Message: r.Message,
                Details: new { r.StartPath }
            ),

            AosWorkspaceNonCompliantException w => new AosErrorEnvelope(
                Code: AosErrorCodes.WorkspaceNonCompliant,
                Message: w.Message,
                Details: new
                {
                    w.AosRootPath,
                    Report = new
                    {
                        w.Report.MissingDirectories,
                        w.Report.InvalidDirectories,
                        w.Report.MissingFiles,
                        w.Report.InvalidFiles,
                        w.Report.ExtraTopLevelEntries
                    }
                }
            ),

            AosIndexRepairFailedException r => new AosErrorEnvelope(
                Code: AosErrorCodes.RepairIndexesFailed,
                Message: r.Message,
                Details: new
                {
                    Issues = r.Issues.Select(i => new { i.ContractPath, i.Message, i.SuggestedFix }).ToArray()
                }
            ),

            ExecutePlanPlanLoadException p => new AosErrorEnvelope(
                Code: AosErrorCodes.ExecutePlanInvalidPlan,
                Message: p.Message,
                Details: p.InnerException is null
                    ? null
                    : new { InnerExceptionType = p.InnerException.GetType().FullName }
            ),

            AosInvalidStateTransitionException t => new AosErrorEnvelope(
                Code: AosErrorCodes.InvalidStateTransition,
                Message: t.Message
            ),

            JsonException j => new AosErrorEnvelope(
                Code: AosErrorCodes.InvalidJson,
                Message: "Invalid JSON.",
                Details: new
                {
                    j.Path,
                    j.LineNumber,
                    j.BytePositionInLine
                }
            ),

            _ => new AosErrorEnvelope(
                Code: AosErrorCodes.UnexpectedInternalError,
                Message: ex.Message,
                Details: new { ExceptionType = ex.GetType().FullName }
            )
        };
    }

    public static AosErrorEnvelope FromWorkspaceValidationReport(AosWorkspaceValidationReport report)
    {
        if (report is null) throw new ArgumentNullException(nameof(report));

        return new AosErrorEnvelope(
            Code: AosErrorCodes.WorkspaceValidationFailed,
            Message: "AOS workspace validation failed.",
            Details: new
            {
                report.RepositoryRootPath,
                report.AosRootPath,
                Issues = report.Issues.Select(i => new
                {
                    i.Layer,
                    i.ContractPath,
                    i.SchemaId,
                    i.InstanceLocation,
                    i.Message
                }).ToArray()
            }
        );
    }
}

