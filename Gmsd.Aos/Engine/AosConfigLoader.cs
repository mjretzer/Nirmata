using System.Text;
using System.Text.Json;

namespace Gmsd.Aos.Engine.Config;

/// <summary>
/// Loads and validates the AOS config document at <c>.aos/config/config.json</c>.
/// </summary>
internal static class AosConfigLoader
{
    public const string ConfigContractPath = ".aos/config/config.json";

    private static readonly Utf8EncodingNoBom Utf8NoBom = new();

    public static AosConfigLoadResult LoadAndValidate(string repositoryRootPath)
    {
        if (repositoryRootPath is null) throw new ArgumentNullException(nameof(repositoryRootPath));

        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");
        var configPath = Path.Combine(aosRootPath, "config", "config.json");

        if (!File.Exists(configPath))
        {
            return new AosConfigLoadResult(
                RepositoryRootPath: repositoryRootPath,
                ConfigPath: configPath,
                Exists: false,
                Config: null,
                Report: new AosConfigValidationReport(ConfigContractPath, Issues: [])
            );
        }

        if (Directory.Exists(configPath))
        {
            return new AosConfigLoadResult(
                RepositoryRootPath: repositoryRootPath,
                ConfigPath: configPath,
                Exists: true,
                Config: null,
                Report: new AosConfigValidationReport(
                    ConfigContractPath,
                    Issues:
                    [
                        new AosConfigValidationIssue(
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
            // Treat config.json as UTF-8 text for deterministic decoding semantics.
            json = File.ReadAllText(configPath, Utf8NoBom.Instance);
        }
        catch (IOException ex)
        {
            return FailedToRead(repositoryRootPath, configPath, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return FailedToRead(repositoryRootPath, configPath, ex.Message);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return new AosConfigLoadResult(
                RepositoryRootPath: repositoryRootPath,
                ConfigPath: configPath,
                Exists: true,
                Config: null,
                Report: new AosConfigValidationReport(
                    ConfigContractPath,
                    Issues:
                    [
                        new AosConfigValidationIssue(
                            JsonPath: "$",
                            Message: $"Invalid JSON: {ex.Message}"
                        )
                    ]
                )
            );
        }

        using (doc)
        {
            var report = AosConfigValidator.Validate(doc.RootElement);
            if (report.Issues.Count > 0)
            {
                return new AosConfigLoadResult(
                    RepositoryRootPath: repositoryRootPath,
                    ConfigPath: configPath,
                    Exists: true,
                    Config: null,
                    Report: report
                );
            }

            var config = AosConfigValidator.Materialize(doc.RootElement);
            return new AosConfigLoadResult(
                RepositoryRootPath: repositoryRootPath,
                ConfigPath: configPath,
                Exists: true,
                Config: config,
                Report: report
            );
        }
    }

    private static AosConfigLoadResult FailedToRead(string repositoryRootPath, string configPath, string message) =>
        new(
            RepositoryRootPath: repositoryRootPath,
            ConfigPath: configPath,
            Exists: true,
            Config: null,
            Report: new AosConfigValidationReport(
                ConfigContractPath,
                Issues:
                [
                    new AosConfigValidationIssue(
                        JsonPath: "$",
                        Message: $"Failed to read config file: {message}"
                    )
                ]
            )
        );

    private sealed class Utf8EncodingNoBom
    {
        public Encoding Instance { get; } = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }
}

internal sealed record AosConfigLoadResult(
    string RepositoryRootPath,
    string ConfigPath,
    bool Exists,
    AosConfigDocument? Config,
    AosConfigValidationReport Report
);

