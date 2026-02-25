using System.Text.Json;
using Gmsd.Aos.Contracts.Secrets;

namespace Gmsd.Aos.Engine.Secrets;

/// <summary>
/// Utility for migrating plaintext API keys from configuration to the secret store.
/// Supports scanning configuration files and migrating keys to secure storage.
/// </summary>
public class SecretMigrationUtility
{
    private readonly ISecretStore _secretStore;
    private readonly List<string> _migrationLog = new();

    public SecretMigrationUtility(ISecretStore secretStore)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    /// <summary>
    /// Migrate plaintext API keys from configuration to the secret store.
    /// Scans configuration for known API key patterns and migrates them.
    /// </summary>
    /// <param name="configJson">The configuration JSON as a string</param>
    /// <returns>Updated configuration with secret references instead of plaintext keys</returns>
    public async Task<string> MigrateConfigurationAsync(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return configJson;

        try
        {
            using var document = JsonDocument.Parse(configJson);
            var updated = await MigrateElementAsync(document.RootElement);
            return updated.GetRawText();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse configuration JSON during migration: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get the migration log showing what was migrated.
    /// </summary>
    public IReadOnlyList<string> GetMigrationLog() => _migrationLog.AsReadOnly();

    /// <summary>
    /// Clear the migration log.
    /// </summary>
    public void ClearMigrationLog() => _migrationLog.Clear();

    private async Task<JsonElement> MigrateElementAsync(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return await MigrateObjectAsync(element);
            case JsonValueKind.Array:
                return await MigrateArrayAsync(element);
            default:
                return element;
        }
    }

    private async Task<JsonElement> MigrateObjectAsync(JsonElement obj)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        foreach (var property in obj.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);

            // Check if this property contains a plaintext API key
            var (isMigrated, migratedElement) = await TryMigratePropertyAsync(property.Name, property.Value);

            if (isMigrated)
            {
                migratedElement.WriteTo(writer);
            }
            else
            {
                var migratedValue = await MigrateElementAsync(property.Value);
                migratedValue.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    private async Task<JsonElement> MigrateArrayAsync(JsonElement array)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();

        foreach (var element in array.EnumerateArray())
        {
            var migratedValue = await MigrateElementAsync(element);
            migratedValue.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    private async Task<(bool, JsonElement)> TryMigratePropertyAsync(string propertyName, JsonElement value)
    {
        // Check if this is an API key property with a plaintext value
        if (!IsApiKeyProperty(propertyName) || value.ValueKind != JsonValueKind.String)
            return (false, value);

        var stringValue = value.GetString();
        if (string.IsNullOrWhiteSpace(stringValue) || IsSecretReference(stringValue))
            return (false, value);

        // This is a plaintext API key - migrate it
        var secretName = DeriveSecretName(propertyName);
        await _secretStore.SetSecretAsync(secretName, stringValue);

        _migrationLog.Add($"Migrated {propertyName} to secret '{secretName}'");

        // Return a secret reference instead
        var secretReference = $"$secret:{secretName}";
        return (true, JsonSerializer.SerializeToElement(secretReference));
    }

    private static bool IsApiKeyProperty(string propertyName)
    {
        var lower = propertyName.ToLowerInvariant();
        return lower.Contains("apikey") || lower.Contains("api_key") || 
               lower.Contains("key") || lower.Contains("token") ||
               lower.Contains("secret") || lower.Contains("password");
    }

    private static bool IsSecretReference(string value)
    {
        return value.StartsWith("$secret:", StringComparison.Ordinal);
    }

    private static string DeriveSecretName(string propertyName)
    {
        // Convert property name to a secret name (lowercase with hyphens)
        var name = System.Text.RegularExpressions.Regex.Replace(
            propertyName, 
            "([a-z])([A-Z])", 
            "$1-$2")
            .ToLowerInvariant();

        return name;
    }
}
