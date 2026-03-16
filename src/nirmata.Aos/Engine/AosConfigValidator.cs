using System.Text.Json;

namespace nirmata.Aos.Engine.Config;

/// <summary>
/// Validates the shape of <c>.aos/config/config.json</c> according to the local schema pack's
/// <c>config.schema.json</c> / <c>secret-ref.schema.json</c> contracts.
///
/// This validator is intentionally minimal and does not require a full JSON Schema evaluation engine.
/// </summary>
internal static class AosConfigValidator
{
    public static AosConfigValidationReport Validate(JsonElement root)
    {
        var issues = new List<AosConfigValidationIssue>();

        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new AosConfigValidationIssue("$", "Config root JSON value must be an object."));
            return new AosConfigValidationReport(AosConfigLoader.ConfigContractPath, issues);
        }

        // additionalProperties: false (config.schema.json)
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name is "schemaVersion" or "secrets")
            {
                continue;
            }

            issues.Add(new AosConfigValidationIssue(
                PropertyPath("$", prop.Name),
                $"Unsupported property '{prop.Name}'. Only 'schemaVersion' and 'secrets' are allowed."
            ));
        }

        // schemaVersion: integer const 1
        if (!root.TryGetProperty("schemaVersion", out var schemaVersionProp))
        {
            issues.Add(new AosConfigValidationIssue("$.schemaVersion", "Missing required 'schemaVersion'."));
        }
        else if (schemaVersionProp.ValueKind != JsonValueKind.Number || !schemaVersionProp.TryGetInt32(out var schemaVersion))
        {
            issues.Add(new AosConfigValidationIssue("$.schemaVersion", "Property 'schemaVersion' must be an integer."));
        }
        else if (schemaVersion != 1)
        {
            issues.Add(new AosConfigValidationIssue("$.schemaVersion", $"Unsupported schemaVersion '{schemaVersion}'. Expected 1."));
        }

        // secrets: object with additionalProperties: SecretRef
        if (!root.TryGetProperty("secrets", out var secretsProp))
        {
            issues.Add(new AosConfigValidationIssue("$.secrets", "Missing required 'secrets' object."));
        }
        else if (secretsProp.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new AosConfigValidationIssue("$.secrets", "Property 'secrets' must be an object."));
        }
        else
        {
            foreach (var secret in secretsProp.EnumerateObject())
            {
                ValidateSecretRef(secret.Name, secret.Value, issues);
            }
        }

        return new AosConfigValidationReport(AosConfigLoader.ConfigContractPath, issues);
    }

    public static AosConfigDocument Materialize(JsonElement root)
    {
        // Caller is expected to validate first; this method throws if the shape is unexpected.
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Config root is not an object.");
        }

        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        var secretsProp = root.GetProperty("secrets");

        var secrets = new Dictionary<string, AosSecretRef>(StringComparer.Ordinal);
        foreach (var secret in secretsProp.EnumerateObject())
        {
            var value = secret.Value;
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Secret '{secret.Name}' is not an object.");
            }

            var kind = value.GetProperty("kind").GetString() ?? "";
            var env = value.TryGetProperty("env", out var envProp) && envProp.ValueKind == JsonValueKind.String
                ? (envProp.GetString() ?? "")
                : "";
            var description = value.TryGetProperty("description", out var descProp) && descProp.ValueKind == JsonValueKind.String
                ? descProp.GetString()
                : null;

            secrets[secret.Name] = new AosSecretRef(kind, env, description);
        }

        return new AosConfigDocument(schemaVersion, secrets);
    }

    private static void ValidateSecretRef(string secretKey, JsonElement value, List<AosConfigValidationIssue> issues)
    {
        var basePath = PropertyPath("$.secrets", secretKey);

        // Secrets-by-reference only: do not allow raw strings.
        if (value.ValueKind == JsonValueKind.String)
        {
            issues.Add(new AosConfigValidationIssue(
                basePath,
                "Plaintext secret values are not allowed. Use a SecretRef object like {\"kind\":\"env\",\"env\":\"MY_ENV_VAR\"}."
            ));
            return;
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new AosConfigValidationIssue(basePath, "Secret value must be an object (SecretRef)."));
            return;
        }

        // additionalProperties: false (secret-ref.schema.json)
        foreach (var prop in value.EnumerateObject())
        {
            if (prop.Name is "kind" or "env" or "description")
            {
                continue;
            }

            issues.Add(new AosConfigValidationIssue(
                $"{basePath}.{prop.Name}",
                $"Unsupported property '{prop.Name}'. Only 'kind', 'env', and 'description' are allowed."
            ));
        }

        // kind: required enum ["env"]
        if (!value.TryGetProperty("kind", out var kindProp))
        {
            issues.Add(new AosConfigValidationIssue($"{basePath}.kind", "Missing required 'kind'."));
            return;
        }

        if (kindProp.ValueKind != JsonValueKind.String)
        {
            issues.Add(new AosConfigValidationIssue($"{basePath}.kind", "Property 'kind' must be a string."));
            return;
        }

        var kind = kindProp.GetString() ?? "";
        if (!string.Equals(kind, "env", StringComparison.Ordinal))
        {
            issues.Add(new AosConfigValidationIssue($"{basePath}.kind", $"Unsupported secret kind '{kind}'. Expected 'env'."));
            return;
        }

        // when kind == env, env is required and must be non-empty string
        if (!value.TryGetProperty("env", out var envProp))
        {
            issues.Add(new AosConfigValidationIssue($"{basePath}.env", "Missing required 'env' for kind 'env'."));
            return;
        }

        if (envProp.ValueKind != JsonValueKind.String)
        {
            issues.Add(new AosConfigValidationIssue($"{basePath}.env", "Property 'env' must be a string."));
            return;
        }

        var env = envProp.GetString() ?? "";
        if (string.IsNullOrWhiteSpace(env))
        {
            issues.Add(new AosConfigValidationIssue($"{basePath}.env", "Property 'env' must be a non-empty string."));
        }

        if (value.TryGetProperty("description", out var descriptionProp) && descriptionProp.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
        {
            issues.Add(new AosConfigValidationIssue($"{basePath}.description", "Property 'description' must be a string when present."));
        }
    }

    private static string EscapePathSegment(string segment)
    {
        // Minimal JSONPath-ish escaping for bracket notation.
        var escaped = (segment ?? "").Replace("'", "\\'", StringComparison.Ordinal);
        return $"['{escaped}']";
    }

    private static string PropertyPath(string parentPath, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            parentPath = "$";
        }

        if (IsSimpleIdentifier(propertyName))
        {
            return $"{parentPath}.{propertyName}";
        }

        return $"{parentPath}{EscapePathSegment(propertyName)}";
    }

    private static bool IsSimpleIdentifier(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return false;
        }

        for (var i = 0; i < propertyName.Length; i++)
        {
            var c = propertyName[i];
            if (i == 0)
            {
                if (!(char.IsLetter(c) || c == '_' || c == '$'))
                {
                    return false;
                }
            }
            else
            {
                if (!(char.IsLetterOrDigit(c) || c == '_' || c == '$'))
                {
                    return false;
                }
            }
        }

        return true;
    }
}

internal sealed record AosConfigValidationReport(
    string ContractPath,
    IReadOnlyList<AosConfigValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

internal sealed record AosConfigValidationIssue(string JsonPath, string Message);

internal sealed record AosConfigDocument(
    int SchemaVersion,
    IReadOnlyDictionary<string, AosSecretRef> Secrets
);

internal sealed record AosSecretRef(
    string Kind,
    string Env,
    string? Description
);

