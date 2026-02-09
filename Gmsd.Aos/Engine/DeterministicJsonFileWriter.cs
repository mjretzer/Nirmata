using System.Buffers;
using System.Text.Json;

namespace Gmsd.Aos.Engine;

/// <summary>
/// Canonical deterministic JSON writer for AOS-emitted artifacts.
/// </summary>
internal static class DeterministicJsonFileWriter
{
    public static void WriteCanonicalJsonTextIfMissing(
        string path,
        string json,
        bool writeIndented = true)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (json is null) throw new ArgumentNullException(nameof(json));

        if (File.Exists(path))
        {
            return;
        }

        using var doc = JsonDocument.Parse(json);
        WriteCanonicalJsonIfMissing(path, doc.RootElement, writeIndented);
    }

    public static void WriteCanonicalJsonTextOverwrite(
        string path,
        string json,
        bool writeIndented = true)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (json is null) throw new ArgumentNullException(nameof(json));

        using var doc = JsonDocument.Parse(json);
        WriteCanonicalJsonOverwrite(path, doc.RootElement, writeIndented);
    }

    public static void WriteCanonicalJsonIfMissing(
        string path,
        JsonElement value,
        bool writeIndented = true)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        if (File.Exists(path))
        {
            return;
        }

        var canonical = CanonicalizeToUtf8Bytes(value, writeIndented);
        WriteAtomicOverwrite(path, canonical, avoidChurn: false);
    }

    public static void WriteCanonicalJsonOverwrite(
        string path,
        JsonElement value,
        bool writeIndented = true)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        var canonical = CanonicalizeToUtf8Bytes(value, writeIndented);
        WriteAtomicOverwrite(path, canonical, avoidChurn: true);
    }

    public static void WriteCanonicalJsonIfMissing<T>(
        string path,
        T value,
        JsonSerializerOptions serializerOptions,
        bool writeIndented = true)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (serializerOptions is null) throw new ArgumentNullException(nameof(serializerOptions));

        if (File.Exists(path))
        {
            return;
        }

        var canonical = SerializeToCanonicalUtf8Bytes(value, serializerOptions, writeIndented);
        WriteAtomicOverwrite(path, canonical, avoidChurn: false);
    }

    public static void WriteCanonicalJsonOverwrite<T>(
        string path,
        T value,
        JsonSerializerOptions serializerOptions,
        bool writeIndented = true)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (serializerOptions is null) throw new ArgumentNullException(nameof(serializerOptions));

        var canonical = SerializeToCanonicalUtf8Bytes(value, serializerOptions, writeIndented);
        WriteAtomicOverwrite(path, canonical, avoidChurn: true);
    }

    internal static byte[] SerializeToCanonicalUtf8Bytes<T>(
        T value,
        JsonSerializerOptions serializerOptions,
        bool writeIndented)
    {
        using var doc = JsonSerializer.SerializeToDocument(value, serializerOptions);
        return CanonicalizeToUtf8Bytes(doc.RootElement, writeIndented);
    }

    internal static byte[] CanonicalizeToUtf8Bytes(JsonElement element, bool writeIndented)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var writerOptions = new JsonWriterOptions
        {
            Indented = writeIndented,
            SkipValidation = false
        };

        using (var writer = new Utf8JsonWriter(buffer, writerOptions))
        {
            WriteCanonicalValue(writer, element);
            writer.Flush();
        }

        var bytes = NormalizeLineEndingsToLf(buffer.WrittenSpan);
        return EnsureTrailingLf(bytes);
    }

    private static void WriteCanonicalValue(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                WriteCanonicalObject(writer, element);
                writer.WriteEndObject();
                break;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteCanonicalValue(writer, item);
                }
                writer.WriteEndArray();
                break;

            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;

            case JsonValueKind.Number:
                // Preserve the serializer's numeric formatting deterministically.
                writer.WriteRawValue(element.GetRawText(), skipInputValidation: true);
                break;

            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;

            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;

            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;

            default:
                throw new InvalidOperationException($"Unsupported JSON value kind '{element.ValueKind}'.");
        }
    }

    private static void WriteCanonicalObject(Utf8JsonWriter writer, JsonElement element)
    {
        var properties = new List<JsonProperty>();
        foreach (var p in element.EnumerateObject())
        {
            properties.Add(p);
        }

        properties.Sort(static (a, b) => StringComparer.Ordinal.Compare(a.Name, b.Name));

        foreach (var p in properties)
        {
            writer.WritePropertyName(p.Name);
            WriteCanonicalValue(writer, p.Value);
        }
    }

    /// <summary>
    /// Atomic overwrite implementation. The optional hook exists solely to make an otherwise
    /// hard-to-test failure window (after temp write, before commit) deterministic in unit tests.
    /// </summary>
    private static void WriteAtomicOverwrite(
        string path,
        byte[] canonicalUtf8,
        bool avoidChurn,
        Action<string>? afterTempWriteBeforeCommit = null)
    {
        if (avoidChurn && File.Exists(path))
        {
            var existing = File.ReadAllBytes(path);
            if (existing.AsSpan().SequenceEqual(canonicalUtf8))
            {
                return;
            }
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = CreateTempSiblingPath(path);
        try
        {
            File.WriteAllBytes(tempPath, canonicalUtf8);
            afterTempWriteBeforeCommit?.Invoke(tempPath);

            if (File.Exists(path))
            {
                // Atomic replace where supported (same volume).
                File.Replace(tempPath, path, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, path, overwrite: true);
            }
        }
        finally
        {
            TryDeleteFile(tempPath);
        }
    }

    internal static void WriteAtomicOverwriteForTest(
        string path,
        byte[] canonicalUtf8,
        bool avoidChurn,
        Action<string> afterTempWriteBeforeCommit)
        => WriteAtomicOverwrite(path, canonicalUtf8, avoidChurn, afterTempWriteBeforeCommit);

    private static string CreateTempSiblingPath(string path)
    {
        var dir = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        var suffix = $".tmp-{Guid.NewGuid():N}";
        return string.IsNullOrWhiteSpace(dir)
            ? $"{name}{suffix}"
            : Path.Combine(dir, $"{name}{suffix}");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static byte[] NormalizeLineEndingsToLf(ReadOnlySpan<byte> bytes)
    {
        var hasCr = false;
        foreach (var b in bytes)
        {
            if (b == (byte)'\r')
            {
                hasCr = true;
                break;
            }
        }

        if (!hasCr)
        {
            return bytes.ToArray();
        }

        var normalized = new byte[bytes.Length];
        var j = 0;
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            if (b == (byte)'\r')
            {
                continue;
            }

            normalized[j++] = b;
        }

        Array.Resize(ref normalized, j);
        return normalized;
    }

    private static byte[] EnsureTrailingLf(byte[] bytes)
    {
        if (bytes.Length == 0)
        {
            return [(byte)'\n'];
        }

        if (bytes[^1] == (byte)'\n')
        {
            return bytes;
        }

        var withLf = new byte[bytes.Length + 1];
        bytes.CopyTo(withLf, 0);
        withLf[^1] = (byte)'\n';
        return withLf;
    }
}

