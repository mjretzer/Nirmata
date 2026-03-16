using System.Buffers;
using System.Text.Json;

namespace nirmata.Aos.Engine;

/// <summary>
/// Canonical deterministic JSON writer for AOS-emitted artifacts.
/// </summary>
internal static class DeterministicJsonFileWriter
{
    private sealed class CrStrippingStream : Stream
    {
        private readonly Stream _inner;
        private readonly bool _leaveOpen;
        private byte? _lastWritten;

        public CrStrippingStream(Stream inner, bool leaveOpen)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _leaveOpen = leaveOpen;
        }

        public void EnsureTrailingLf()
        {
            if (_lastWritten == (byte)'\n')
            {
                return;
            }

            _inner.WriteByte((byte)'\n');
            _lastWritten = (byte)'\n';
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            // Fast path: no CR in span
            var hasCr = false;
            foreach (var b in buffer)
            {
                if (b == (byte)'\r')
                {
                    hasCr = true;
                    break;
                }
            }

            if (!hasCr)
            {
                _inner.Write(buffer);
                if (buffer.Length > 0)
                {
                    _lastWritten = buffer[^1];
                }
                return;
            }

            // Slow path: strip CR bytes.
            Span<byte> scratch = buffer.Length <= 4096 ? stackalloc byte[buffer.Length] : new byte[buffer.Length];
            var j = 0;
            for (var i = 0; i < buffer.Length; i++)
            {
                var b = buffer[i];
                if (b == (byte)'\r')
                {
                    continue;
                }
                scratch[j++] = b;
            }

            if (j > 0)
            {
                _inner.Write(scratch[..j]);
                _lastWritten = scratch[j - 1];
            }
        }

        public override void Write(byte[] buffer, int offset, int count) =>
            Write(new ReadOnlySpan<byte>(buffer, offset, count));

        public override void Flush() => _inner.Flush();

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && !_leaveOpen)
            {
                _inner.Dispose();
            }
        }
    }

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

        WriteAtomicOverwrite(
            path,
            writeCanonicalUtf8ToStream: stream => WriteCanonicalJsonToStream(stream, value, writeIndented),
            avoidChurn: false);
    }

    public static void WriteCanonicalJsonOverwrite(
        string path,
        JsonElement value,
        bool writeIndented = true)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));

        WriteAtomicOverwrite(
            path,
            writeCanonicalUtf8ToStream: stream => WriteCanonicalJsonToStream(stream, value, writeIndented),
            avoidChurn: true);
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

        WriteAtomicOverwrite(
            path,
            writeCanonicalUtf8ToStream: stream =>
            {
                using var doc = JsonSerializer.SerializeToDocument(value, serializerOptions);
                WriteCanonicalJsonToStream(stream, doc.RootElement, writeIndented);
            },
            avoidChurn: false);
    }

    public static void WriteCanonicalJsonOverwrite<T>(
        string path,
        T value,
        JsonSerializerOptions serializerOptions,
        bool writeIndented = true)
    {
        if (path is null) throw new ArgumentNullException(nameof(path));
        if (serializerOptions is null) throw new ArgumentNullException(nameof(serializerOptions));

        WriteAtomicOverwrite(
            path,
            writeCanonicalUtf8ToStream: stream =>
            {
                using var doc = JsonSerializer.SerializeToDocument(value, serializerOptions);
                WriteCanonicalJsonToStream(stream, doc.RootElement, writeIndented);
            },
            avoidChurn: true);
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
    /// Atomic overwrite implementation.
    /// </summary>
    private static void WriteAtomicOverwrite(
        string path,
        byte[] canonicalUtf8,
        bool avoidChurn,
        Action<string>? afterTempWriteBeforeCommit = null)
    {
        WriteAtomicOverwrite(
            path,
            writeCanonicalUtf8ToStream: stream => stream.Write(canonicalUtf8, 0, canonicalUtf8.Length),
            avoidChurn: avoidChurn,
            afterTempWriteBeforeCommit: afterTempWriteBeforeCommit);
    }

    private static void WriteAtomicOverwrite(
        string path,
        Action<Stream> writeCanonicalUtf8ToStream,
        bool avoidChurn,
        Action<string>? afterTempWriteBeforeCommit = null)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var tempPath = CreateTempSiblingPath(path);
        try
        {
            using (var tempStream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                writeCanonicalUtf8ToStream(tempStream);
                tempStream.Flush(flushToDisk: true);
            }

            afterTempWriteBeforeCommit?.Invoke(tempPath);

            if (avoidChurn && File.Exists(path))
            {
                if (IsIdenticalStreaming(path, tempPath))
                {
                    return;
                }
            }

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

    private static void WriteCanonicalJsonToStream(Stream stream, JsonElement element, bool writeIndented)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));

        var writerOptions = new JsonWriterOptions
        {
            Indented = writeIndented,
            SkipValidation = false
        };

        using var normalizing = new CrStrippingStream(stream, leaveOpen: true);
        using (var writer = new Utf8JsonWriter(normalizing, writerOptions))
        {
            WriteCanonicalValue(writer, element);
            writer.Flush();
        }

        normalizing.EnsureTrailingLf();
    }

    private static bool IsIdenticalStreaming(string existingPath, string newPath)
    {
        try
        {
            using var existing = new FileStream(existingPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var newer = new FileStream(newPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (existing.Length != newer.Length)
            {
                return false;
            }

            const int chunkSize = 4096;
            var existingBuffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            var newBuffer = ArrayPool<byte>.Shared.Rent(chunkSize);
            try
            {
                while (true)
                {
                    var existingRead = existing.Read(existingBuffer, 0, chunkSize);
                    var newRead = newer.Read(newBuffer, 0, chunkSize);

                    if (existingRead != newRead)
                    {
                        return false;
                    }

                    if (existingRead == 0)
                    {
                        return true;
                    }

                    var existingSpan = new ReadOnlySpan<byte>(existingBuffer, 0, existingRead);
                    var newSpan = new ReadOnlySpan<byte>(newBuffer, 0, newRead);
                    if (!existingSpan.SequenceEqual(newSpan))
                    {
                        return false;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(existingBuffer);
                ArrayPool<byte>.Shared.Return(newBuffer);
            }
        }
        catch
        {
            return false;
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

