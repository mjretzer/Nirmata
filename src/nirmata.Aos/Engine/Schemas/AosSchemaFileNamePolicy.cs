using System.Text;

namespace nirmata.Aos.Engine.Schemas;

internal static class AosSchemaFileNamePolicy
{
    internal const string SchemaSuffix = ".schema.json";

    internal static void EnsureCanonicalSchemaFileName(string fileName, string sourceLabel)
    {
        if (!fileName.EndsWith(SchemaSuffix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Schema filename '{fileName}' ({sourceLabel}) does not end with '{SchemaSuffix}'."
            );
        }

        var baseName = fileName[..^SchemaSuffix.Length];
        var expectedBaseName = CanonicalizeSchemaBaseName(baseName);
        var expectedFileName = expectedBaseName + SchemaSuffix;

        if (string.Equals(fileName, expectedFileName, StringComparison.Ordinal))
        {
            return;
        }

        throw new InvalidOperationException(
            $"Non-canonical schema filename '{fileName}' ({sourceLabel}). " +
            $"Expected '{expectedFileName}'. " +
            $"Canonical form is lower-kebab-case base name with suffix '{SchemaSuffix}' and no additional '.' characters."
        );
    }

    internal static string CanonicalizeSchemaBaseName(string baseName)
    {
        // Canonical: lower-kebab-case (letters/digits separated by single '-'), no '.' characters.
        // Example: `context.pack` -> `context-pack`
        var lower = baseName.ToLowerInvariant();

        var sb = new StringBuilder(lower.Length);
        var lastWasDash = false;

        foreach (var ch in lower)
        {
            var isAlphaNum = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9');
            if (isAlphaNum)
            {
                sb.Append(ch);
                lastWasDash = false;
                continue;
            }

            // Treat any non-alphanumeric as a separator and collapse runs to a single '-'.
            if (sb.Length == 0 || lastWasDash)
            {
                continue;
            }

            sb.Append('-');
            lastWasDash = true;
        }

        // Trim trailing dash.
        if (sb.Length > 0 && sb[^1] == '-')
        {
            sb.Length--;
        }

        if (sb.Length == 0)
        {
            // Defensive; should never happen for our shipped schemas.
            throw new InvalidOperationException($"Unable to canonicalize schema base name '{baseName}'.");
        }

        return sb.ToString();
    }

    internal static string ToSchemaId(string fileName)
    {
        if (!fileName.EndsWith(SchemaSuffix, StringComparison.Ordinal))
        {
            return fileName;
        }

        return fileName[..^SchemaSuffix.Length];
    }
}

