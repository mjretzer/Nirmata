using System.Text.Json;
using Gmsd.Aos.Engine.Paths;

namespace Gmsd.Aos.Engine.Stores;

internal abstract class AosJsonStoreBase : IAosJsonStore
{
    private readonly string _contractPathPrefix;

    protected AosJsonStoreBase(string aosRootPath, string contractPathPrefix)
    {
        if (aosRootPath is null) throw new ArgumentNullException(nameof(aosRootPath));
        if (contractPathPrefix is null) throw new ArgumentNullException(nameof(contractPathPrefix));

        if (!contractPathPrefix.StartsWith(".aos/", StringComparison.Ordinal))
        {
            throw new ArgumentException("Contract prefix must start with '.aos/'.", nameof(contractPathPrefix));
        }

        // Keep prefixes normalized and deterministic.
        _contractPathPrefix = contractPathPrefix.EndsWith("/", StringComparison.Ordinal)
            ? contractPathPrefix
            : $"{contractPathPrefix}/";

        AosRootPath = aosRootPath;
    }

    public string AosRootPath { get; }

    public bool Exists(string contractPath)
    {
        var fullPath = ResolveFilePath(contractPath);
        return File.Exists(fullPath) && !Directory.Exists(fullPath);
    }

    public JsonElement ReadJsonElement(string contractPath)
    {
        var fullPath = ResolveFilePath(contractPath);
        using var stream = File.OpenRead(fullPath);
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }

    public T ReadJson<T>(string contractPath, JsonSerializerOptions serializerOptions)
    {
        if (serializerOptions is null) throw new ArgumentNullException(nameof(serializerOptions));

        var fullPath = ResolveFilePath(contractPath);
        var json = File.ReadAllText(fullPath);
        var value = JsonSerializer.Deserialize<T>(json, serializerOptions);
        if (value is null)
        {
            throw new InvalidOperationException($"Failed to deserialize JSON from '{contractPath}'.");
        }

        return value;
    }

    public void WriteJsonIfMissing<T>(string contractPath, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        if (serializerOptions is null) throw new ArgumentNullException(nameof(serializerOptions));

        var fullPath = ResolveFilePath(contractPath);
        DeterministicJsonFileWriter.WriteCanonicalJsonIfMissing(fullPath, value, serializerOptions, writeIndented);
    }

    public void WriteJsonOverwrite<T>(string contractPath, T value, JsonSerializerOptions serializerOptions, bool writeIndented = true)
    {
        if (serializerOptions is null) throw new ArgumentNullException(nameof(serializerOptions));

        var fullPath = ResolveFilePath(contractPath);
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(fullPath, value, serializerOptions, writeIndented);
    }

    public void DeleteFile(string contractPath)
    {
        var fullPath = ResolveFilePath(contractPath);

        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"Refusing to delete directory at '{contractPath}'.");
        }

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    protected string ResolveFilePath(string contractPath)
    {
        if (contractPath is null) throw new ArgumentNullException(nameof(contractPath));
        EnsureInStorePrefix(contractPath);
        return AosPathRouter.ToAosRootPath(AosRootPath, contractPath);
    }

    private void EnsureInStorePrefix(string contractPath)
    {
        if (!contractPath.StartsWith(_contractPathPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Contract path '{contractPath}' is not within required prefix '{_contractPathPrefix}'.",
                nameof(contractPath)
            );
        }
    }
}

