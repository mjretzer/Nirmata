using System.Text.Json;
using nirmata.Aos.Contracts.State;
using nirmata.Aos.Engine;
using nirmata.Aos.Engine.Paths;

namespace nirmata.Agents.Execution.Continuity;

/// <summary>
/// File-based implementation of <see cref="IHandoffStateStore"/> for <c>.aos/state/handoff.json</c>.
/// Uses deterministic JSON serialization for stable output.
/// </summary>
public sealed class HandoffStateStore : IHandoffStateStore
{
    private readonly string _aosRootPath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public const string HandoffContractPath = ".aos/state/handoff.json";

    /// <summary>
    /// Initializes a new instance of the <see cref="HandoffStateStore"/> class.
    /// </summary>
    /// <param name="aosRootPath">The root path of the AOS workspace.</param>
    public HandoffStateStore(string aosRootPath)
    {
        _aosRootPath = aosRootPath ?? throw new ArgumentNullException(nameof(aosRootPath));
    }

    /// <inheritdoc />
    public string HandoffPath => AosPathRouter.ToAosRootPath(_aosRootPath, HandoffContractPath);

    /// <inheritdoc />
    public bool Exists()
    {
        return File.Exists(HandoffPath);
    }

    /// <inheritdoc />
    public HandoffState ReadHandoff()
    {
        if (!Exists())
        {
            throw new FileNotFoundException("Handoff file not found.", HandoffPath);
        }

        var json = File.ReadAllText(HandoffPath);
        var handoff = JsonSerializer.Deserialize<HandoffState>(json, JsonOptions);

        if (handoff is null)
        {
            throw new InvalidOperationException($"Failed to deserialize handoff state from '{HandoffContractPath}'.");
        }

        return handoff;
    }

    /// <inheritdoc />
    public void WriteHandoff(HandoffState handoff)
    {
        if (handoff is null) throw new ArgumentNullException(nameof(handoff));

        var fullPath = HandoffPath;
        var dir = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Use deterministic JSON writer for stable output
        DeterministicJsonFileWriter.WriteCanonicalJsonOverwrite(
            fullPath,
            handoff,
            JsonOptions,
            writeIndented: true
        );
    }

    /// <inheritdoc />
    public void DeleteHandoff()
    {
        if (Exists())
        {
            File.Delete(HandoffPath);
        }
    }
}
