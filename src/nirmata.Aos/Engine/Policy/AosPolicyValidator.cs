using System.Text.Json;

namespace nirmata.Aos.Engine.Policy;

/// <summary>
/// Validates the shape of <c>.aos/config/policy.json</c> according to the embedded <c>policy.schema.json</c>.
///
/// This validator is intentionally minimal and does not require a full JSON Schema evaluation engine.
/// </summary>
internal static class AosPolicyValidator
{
    public static AosPolicyValidationReport Validate(JsonElement root)
    {
        var issues = new List<AosPolicyValidationIssue>();

        if (root.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new AosPolicyValidationIssue("$", "Policy root JSON value must be an object."));
            return new AosPolicyValidationReport(AosPolicyLoader.PolicyContractPath, issues);
        }

        // additionalProperties: false (policy.schema.json)
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Name is "schemaVersion" or "scopeAllowlist" or "toolAllowlist" or "noImplicitState")
            {
                continue;
            }

            issues.Add(new AosPolicyValidationIssue(
                PropertyPath("$", prop.Name),
                $"Unsupported property '{prop.Name}'. Only 'schemaVersion', 'scopeAllowlist', 'toolAllowlist', and 'noImplicitState' are allowed."
            ));
        }

        // schemaVersion: integer const 1
        if (!root.TryGetProperty("schemaVersion", out var schemaVersionProp))
        {
            issues.Add(new AosPolicyValidationIssue("$.schemaVersion", "Missing required 'schemaVersion'."));
        }
        else if (schemaVersionProp.ValueKind != JsonValueKind.Number || !schemaVersionProp.TryGetInt32(out var schemaVersion))
        {
            issues.Add(new AosPolicyValidationIssue("$.schemaVersion", "Property 'schemaVersion' must be an integer."));
        }
        else if (schemaVersion != 1)
        {
            issues.Add(new AosPolicyValidationIssue("$.schemaVersion", $"Unsupported schemaVersion '{schemaVersion}'. Expected 1."));
        }

        // noImplicitState: boolean const true
        if (!root.TryGetProperty("noImplicitState", out var noImplicitStateProp))
        {
            issues.Add(new AosPolicyValidationIssue("$.noImplicitState", "Missing required 'noImplicitState'."));
        }
        else if (noImplicitStateProp.ValueKind != JsonValueKind.True && noImplicitStateProp.ValueKind != JsonValueKind.False)
        {
            issues.Add(new AosPolicyValidationIssue("$.noImplicitState", "Property 'noImplicitState' must be a boolean."));
        }
        else if (!noImplicitStateProp.GetBoolean())
        {
            issues.Add(new AosPolicyValidationIssue("$.noImplicitState", "Property 'noImplicitState' must be true."));
        }

        // scopeAllowlist: object { write: string[] (min 1) }
        if (!root.TryGetProperty("scopeAllowlist", out var scopeAllowlistProp))
        {
            issues.Add(new AosPolicyValidationIssue("$.scopeAllowlist", "Missing required 'scopeAllowlist' object."));
        }
        else if (scopeAllowlistProp.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new AosPolicyValidationIssue("$.scopeAllowlist", "Property 'scopeAllowlist' must be an object."));
        }
        else
        {
            // additionalProperties: false
            foreach (var prop in scopeAllowlistProp.EnumerateObject())
            {
                if (prop.Name is "write")
                {
                    continue;
                }
                issues.Add(new AosPolicyValidationIssue(
                    PropertyPath("$.scopeAllowlist", prop.Name),
                    $"Unsupported property '{prop.Name}'. Only 'write' is allowed."
                ));
            }

            if (!scopeAllowlistProp.TryGetProperty("write", out var writeProp))
            {
                issues.Add(new AosPolicyValidationIssue("$.scopeAllowlist.write", "Missing required 'write' allowlist array."));
            }
            else if (writeProp.ValueKind != JsonValueKind.Array)
            {
                issues.Add(new AosPolicyValidationIssue("$.scopeAllowlist.write", "Property 'write' must be an array."));
            }
            else if (writeProp.GetArrayLength() == 0)
            {
                issues.Add(new AosPolicyValidationIssue("$.scopeAllowlist.write", "Property 'write' must contain at least one entry."));
            }
            else
            {
                var idx = 0;
                foreach (var item in writeProp.EnumerateArray())
                {
                    var itemPath = $"$.scopeAllowlist.write[{idx}]";
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        issues.Add(new AosPolicyValidationIssue(itemPath, "Allowlist entry must be a string."));
                    }
                    else
                    {
                        var value = (item.GetString() ?? "").Trim();
                        if (string.IsNullOrWhiteSpace(value))
                        {
                            issues.Add(new AosPolicyValidationIssue(itemPath, "Allowlist entry must be a non-empty string."));
                        }
                        else if (Path.IsPathFullyQualified(value) || Path.IsPathRooted(value))
                        {
                            issues.Add(new AosPolicyValidationIssue(itemPath, "Allowlist entry must be a relative path (not rooted)."));
                        }
                        else if (ContainsDotDotSegment(value))
                        {
                            issues.Add(new AosPolicyValidationIssue(itemPath, "Allowlist entry must not contain '..' path traversal segments."));
                        }
                    }

                    idx++;
                }
            }
        }

        // toolAllowlist: object { tools: string[], providers: string[] }
        if (!root.TryGetProperty("toolAllowlist", out var toolAllowlistProp))
        {
            issues.Add(new AosPolicyValidationIssue("$.toolAllowlist", "Missing required 'toolAllowlist' object."));
        }
        else if (toolAllowlistProp.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new AosPolicyValidationIssue("$.toolAllowlist", "Property 'toolAllowlist' must be an object."));
        }
        else
        {
            // additionalProperties: false
            foreach (var prop in toolAllowlistProp.EnumerateObject())
            {
                if (prop.Name is "tools" or "providers")
                {
                    continue;
                }

                issues.Add(new AosPolicyValidationIssue(
                    PropertyPath("$.toolAllowlist", prop.Name),
                    $"Unsupported property '{prop.Name}'. Only 'tools' and 'providers' are allowed."
                ));
            }

            ValidateStringArrayRequired(toolAllowlistProp, "$.toolAllowlist.tools", "tools", issues);
            ValidateStringArrayRequired(toolAllowlistProp, "$.toolAllowlist.providers", "providers", issues);
        }

        return new AosPolicyValidationReport(AosPolicyLoader.PolicyContractPath, issues);
    }

    public static AosPolicyDocument Materialize(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Policy root is not an object.");
        }

        var schemaVersion = root.GetProperty("schemaVersion").GetInt32();
        var noImplicitState = root.GetProperty("noImplicitState").GetBoolean();

        var scopeAllowlistProp = root.GetProperty("scopeAllowlist");
        var writeProp = scopeAllowlistProp.GetProperty("write");
        var write = writeProp
            .EnumerateArray()
            .Select(v => (v.GetString() ?? "").Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();

        var toolAllowlistProp = root.GetProperty("toolAllowlist");
        var tools = toolAllowlistProp.GetProperty("tools")
            .EnumerateArray()
            .Select(v => (v.GetString() ?? "").Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();
        var providers = toolAllowlistProp.GetProperty("providers")
            .EnumerateArray()
            .Select(v => (v.GetString() ?? "").Trim())
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .ToArray();

        return new AosPolicyDocument(
            SchemaVersion: schemaVersion,
            ScopeAllowlist: new AosPolicyScopeAllowlist(Write: write),
            ToolAllowlist: new AosPolicyToolAllowlist(Tools: tools, Providers: providers),
            NoImplicitState: noImplicitState
        );
    }

    private static void ValidateStringArrayRequired(
        JsonElement obj,
        string basePath,
        string propertyName,
        List<AosPolicyValidationIssue> issues)
    {
        if (!obj.TryGetProperty(propertyName, out var prop))
        {
            issues.Add(new AosPolicyValidationIssue(basePath, $"Missing required '{propertyName}' array."));
            return;
        }

        if (prop.ValueKind != JsonValueKind.Array)
        {
            issues.Add(new AosPolicyValidationIssue(basePath, $"Property '{propertyName}' must be an array."));
            return;
        }

        var idx = 0;
        foreach (var item in prop.EnumerateArray())
        {
            var itemPath = $"{basePath}[{idx}]";
            if (item.ValueKind != JsonValueKind.String)
            {
                issues.Add(new AosPolicyValidationIssue(itemPath, "Allowlist entry must be a string."));
            }
            else
            {
                var value = (item.GetString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(value))
                {
                    issues.Add(new AosPolicyValidationIssue(itemPath, "Allowlist entry must be a non-empty string."));
                }
            }
            idx++;
        }
    }

    private static bool ContainsDotDotSegment(string value)
    {
        var segments = value.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var seg in segments)
        {
            if (string.Equals(seg, "..", StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string EscapePathSegment(string segment)
    {
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

internal sealed record AosPolicyValidationReport(
    string ContractPath,
    IReadOnlyList<AosPolicyValidationIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

internal sealed record AosPolicyValidationIssue(string JsonPath, string Message);

internal sealed record AosPolicyDocument(
    int SchemaVersion,
    AosPolicyScopeAllowlist ScopeAllowlist,
    AosPolicyToolAllowlist ToolAllowlist,
    bool NoImplicitState
);

internal sealed record AosPolicyScopeAllowlist(IReadOnlyList<string> Write);

internal sealed record AosPolicyToolAllowlist(IReadOnlyList<string> Tools, IReadOnlyList<string> Providers);

