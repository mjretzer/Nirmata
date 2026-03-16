using System.Text;
using System.Text.Json;

namespace nirmata.Aos.Engine.Policy;

/// <summary>
/// Loads and validates the AOS policy document at <c>.aos/config/policy.json</c>.
/// </summary>
internal static class AosPolicyLoader
{
    public const string PolicyContractPath = ".aos/config/policy.json";

    private static readonly Utf8EncodingNoBom Utf8NoBom = new();

    public static AosPolicyLoadResult LoadAndValidate(string repositoryRootPath)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        var policyPath = Path.Combine(aosRootPath, "config", "policy.json");

        if (!File.Exists(policyPath))
        {
            return new AosPolicyLoadResult(
                RepositoryRootPath: repositoryRootPath,
                PolicyPath: policyPath,
                Exists: false,
                Policy: null,
                Report: new AosPolicyValidationReport(PolicyContractPath, Issues: [])
            );
        }

        if (Directory.Exists(policyPath))
        {
            return new AosPolicyLoadResult(
                RepositoryRootPath: repositoryRootPath,
                PolicyPath: policyPath,
                Exists: true,
                Policy: null,
                Report: new AosPolicyValidationReport(
                    PolicyContractPath,
                    Issues:
                    [
                        new AosPolicyValidationIssue(
                            JsonPath: "$",
                            Message: "Expected file, found directory."
                        )
                    ]
                )
            );
        }

        string json;
        try
        {
            // Treat policy.json as UTF-8 text for deterministic decoding semantics.
            json = File.ReadAllText(policyPath, Utf8NoBom.Instance);
        }
        catch (IOException ex)
        {
            return FailedToRead(repositoryRootPath, policyPath, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailedToRead(repositoryRootPath, policyPath, ex.Message);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new AosPolicyLoadResult(
                RepositoryRootPath: repositoryRootPath,
                PolicyPath: policyPath,
                Exists: true,
                Policy: null,
                Report: new AosPolicyValidationReport(
                    PolicyContractPath,
                    Issues:
                    [
                        new AosPolicyValidationIssue(
                            JsonPath: "$",
                            Message: $"Invalid JSON: {ex.Message}"
                        )
                    ]
                )
            );
        }

        using (doc)
        {
            var report = AosPolicyValidator.Validate(doc.RootElement);
            if (report.Issues.Count > 0)
            {
                return new AosPolicyLoadResult(
                    RepositoryRootPath: repositoryRootPath,
                    PolicyPath: policyPath,
                    Exists: true,
                    Policy: null,
                    Report: report
                );
            }

            var policy = AosPolicyValidator.Materialize(doc.RootElement);
            return new AosPolicyLoadResult(
                RepositoryRootPath: repositoryRootPath,
                PolicyPath: policyPath,
                Exists: true,
                Policy: policy,
                Report: report
            );
        }
    }

    private static AosPolicyLoadResult FailedToRead(string repositoryRootPath, string policyPath, string message) =>
        new(
            RepositoryRootPath: repositoryRootPath,
            PolicyPath: policyPath,
            Exists: true,
            Policy: null,
            Report: new AosPolicyValidationReport(
                PolicyContractPath,
                Issues:
                [
                    new AosPolicyValidationIssue(
                        JsonPath: "$",
                        Message: $"Failed to read policy file: {message}"
                    )
                ]
            )
        );

    private sealed class Utf8EncodingNoBom
    {
        public Encoding Instance { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}

internal sealed record AosPolicyLoadResult(
    string RepositoryRootPath,
    string PolicyPath,
    bool Exists,
    AosPolicyDocument? Policy,
    AosPolicyValidationReport Report
);

