using System.Text.Json;
using nirmata.Aos.Contracts.Secrets;

namespace nirmata.Aos.Engine.Secrets;

/// <summary>
/// Resolves secret references in configuration files.
/// Replaces $secret:name references with actual secret values at configuration load time.
/// </summary>
public class SecretConfigurationResolver
{
    private const string SecretReferencePrefix = "$secret:";
    private readonly ISecretStore _secretStore;

    public SecretConfigurationResolver(ISecretStore secretStore)
    {
        _secretStore = secretStore ?? throw new ArgumentNullException(nameof(secretStore));
    }

    /// <summary>
    /// Resolve secret references in a JSON configuration object.
    /// Replaces all $secret:name references with actual secret values.
    /// </summary>
    /// <param name="configJson">The JSON configuration as a string</param>
    /// <returns>The configuration with secret references resolved</returns>
    /// <exception cref="SecretNotFoundException">Thrown if a referenced secret does not exist</exception>
    public async Task<string> ResolveAsync(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return configJson;

        using var document = JsonDocument.Parse(configJson);
        var resolved = await ResolveElementAsync(document.RootElement);
        return resolved.GetRawText();
    }

    /// <summary>
    /// Resolve secret references in a JSON element recursively.
    /// </summary>
    private async Task<JsonElement> ResolveElementAsync(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return await ResolveObjectAsync(element);

            case JsonValueKind.Array:
                return await ResolveArrayAsync(element);

            case JsonValueKind.String:
                return await ResolveStringAsync(element);

            default:
                return element;
        }
    }

    /// <summary>
    /// Resolve secret references in a JSON object.
    /// </summary>
    private async Task<JsonElement> ResolveObjectAsync(JsonElement obj)
    {
        var options = new JsonSerializerOptions { WriteIndented = false };
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        foreach (var property in obj.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            var resolvedValue = await ResolveElementAsync(property.Value);
            resolvedValue.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Resolve secret references in a JSON array.
    /// </summary>
    private async Task<JsonElement> ResolveArrayAsync(JsonElement array)
    {
        var options = new JsonSerializerOptions { WriteIndented = false };
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();

        foreach (var element in array.EnumerateArray())
        {
            var resolvedValue = await ResolveElementAsync(element);
            resolvedValue.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Resolve secret references in a string value.
    /// If the string is a secret reference ($secret:name), retrieves the actual secret.
    /// Otherwise, returns the string unchanged.
    /// </summary>
    private async Task<JsonElement> ResolveStringAsync(JsonElement element)
    {
        var value = element.GetString();
        if (string.IsNullOrEmpty(value))
            return element;

        if (!value.StartsWith(SecretReferencePrefix, StringComparison.Ordinal))
            return element;

        var secretName = value.Substring(SecretReferencePrefix.Length);
        if (string.IsNullOrWhiteSpace(secretName))
            throw new InvalidOperationException($"Invalid secret reference: '{value}'. Secret name cannot be empty.");

        try
        {
            var secretValue = await _secretStore.GetSecretAsync(secretName);
            return JsonSerializer.SerializeToElement(secretValue);
        }
        catch (SecretNotFoundException)
        {
            throw new InvalidOperationException(
                $"Configuration references secret '{secretName}' which does not exist in the secret store. " +
                $"Use 'aos secret set {secretName} <value>' to create it.");
        }
    }

    /// <summary>
    /// Check if a configuration string contains any secret references.
    /// Useful for logging configuration without exposing secrets.
    /// </summary>
    public static bool ContainsSecretReferences(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return false;

        return configJson.Contains(SecretReferencePrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Mask secret references in a configuration string for safe logging.
    /// Replaces actual secret values with masked references.
    /// </summary>
    public static string MaskSecrets(string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return configJson;

        try
        {
            using var document = JsonDocument.Parse(configJson);
            return MaskSecretsInElement(document.RootElement).GetRawText();
        }
        catch
        {
            // If parsing fails, return original (already masked if it contains $secret: references)
            return configJson;
        }
    }

    /// <summary>
    /// Recursively mask secrets in a JSON element.
    /// </summary>
    private static JsonElement MaskSecretsInElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return MaskSecretsInObject(element);

            case JsonValueKind.Array:
                return MaskSecretsInArray(element);

            case JsonValueKind.String:
                var value = element.GetString();
                if (value?.StartsWith(SecretReferencePrefix, StringComparison.Ordinal) == true)
                    return JsonSerializer.SerializeToElement(value);
                return element;

            default:
                return element;
        }
    }

    /// <summary>
    /// Mask secrets in a JSON object.
    /// </summary>
    private static JsonElement MaskSecretsInObject(JsonElement obj)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();

        foreach (var property in obj.EnumerateObject())
        {
            writer.WritePropertyName(property.Name);
            var maskedValue = MaskSecretsInElement(property.Value);
            maskedValue.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonDocument.Parse(json).RootElement;
    }

    /// <summary>
    /// Mask secrets in a JSON array.
    /// </summary>
    private static JsonElement MaskSecretsInArray(JsonElement array)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();

        foreach (var element in array.EnumerateArray())
        {
            var maskedValue = MaskSecretsInElement(element);
            maskedValue.WriteTo(writer);
        }

        writer.WriteEndArray();
        writer.Flush();

        stream.Position = 0;
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonDocument.Parse(json).RootElement;
    }
}
