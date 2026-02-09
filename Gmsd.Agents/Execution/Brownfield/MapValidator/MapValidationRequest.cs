namespace Gmsd.Agents.Execution.Brownfield.MapValidator;

/// <summary>
/// Request for map validation operation.
/// </summary>
public sealed class MapValidationRequest
{
    /// <summary>
    /// Absolute path to the repository root containing the .aos/codebase directory.
    /// </summary>
    public string RepositoryRootPath { get; init; } = "";

    /// <summary>
    /// Whether to perform cross-file invariant checks. Default is true.
    /// </summary>
    public bool CheckCrossFileInvariants { get; init; } = true;

    /// <summary>
    /// Whether to validate schema compliance for all artifacts. Default is true.
    /// </summary>
    public bool ValidateSchemaCompliance { get; init; } = true;

    /// <summary>
    /// Whether to validate determinism by computing and comparing file hashes. Default is false.
    /// </summary>
    public bool ValidateDeterminism { get; init; } = false;

    /// <summary>
    /// Optional dictionary of expected SHA256 hashes for each artifact (key: artifact name, value: hex hash).
    /// When provided, actual file hashes are compared against these expected values.
    /// </summary>
    public IReadOnlyDictionary<string, string>? ExpectedHashes { get; init; }

    /// <summary>
    /// Optional list of specific artifacts to validate. If empty, validates all artifacts.
    /// </summary>
    public IReadOnlyList<string> SpecificArtifacts { get; init; } = Array.Empty<string>();
}
