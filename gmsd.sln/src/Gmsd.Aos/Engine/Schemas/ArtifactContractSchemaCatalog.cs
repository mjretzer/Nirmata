namespace Gmsd.Aos.Engine.Schemas;

internal static class ArtifactContractSchemaCatalog
{
    internal const string TaskPlanSchemaId = "gmsd:aos:schema:task-plan:v1";
    internal const string CommandProposalSchemaId = "gmsd:aos:schema:command-proposal:v1";

    internal const string TaskPlanSchemaFileName = "task-plan.schema.json";
    internal const string CommandProposalSchemaFileName = "command-proposal.schema.json";

    internal static IReadOnlyList<ArtifactContractMetadata> RequiredContracts { get; } =
    [
        new ArtifactContractMetadata(
            SchemaId: TaskPlanSchemaId,
            SchemaFileName: TaskPlanSchemaFileName,
            CurrentVersion: 1,
            SupportedVersions: [1],
            DeprecatedVersions: []),
        new ArtifactContractMetadata(
            SchemaId: CommandProposalSchemaId,
            SchemaFileName: CommandProposalSchemaFileName,
            CurrentVersion: 1,
            SupportedVersions: [1],
            DeprecatedVersions: [])
    ];

    internal sealed record ArtifactContractMetadata(
        string SchemaId,
        string SchemaFileName,
        int CurrentVersion,
        IReadOnlyList<int> SupportedVersions,
        IReadOnlyList<int> DeprecatedVersions);
}
