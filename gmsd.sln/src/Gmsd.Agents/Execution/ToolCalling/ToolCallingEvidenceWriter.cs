using System.Text.Json;
using Gmsd.Aos.Public;

namespace Gmsd.Agents.Execution.ToolCalling;

/// <summary>
/// Interface for writing tool calling conversation evidence to the evidence store.
/// </summary>
public interface IToolCallingEvidenceWriter
{
    /// <summary>
    /// Writes the tool calling conversation evidence to the evidence store.
    /// </summary>
    /// <param name="evidence">The evidence to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The absolute path to the written file.</returns>
    Task<string> WriteAsync(ToolCallingConversationEvidence evidence, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default implementation of the tool calling evidence writer.
/// Writes evidence to the AOS evidence directory structure.
/// </summary>
public sealed class ToolCallingEvidenceWriter : IToolCallingEvidenceWriter
{
    private readonly IWorkspace _workspace;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCallingEvidenceWriter"/> class.
    /// </summary>
    /// <param name="workspace">The workspace for accessing evidence directories.</param>
    public ToolCallingEvidenceWriter(IWorkspace workspace)
    {
        _workspace = workspace ?? throw new ArgumentNullException(nameof(workspace));
    }

    /// <inheritdoc />
    public async Task<string> WriteAsync(ToolCallingConversationEvidence evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var evidenceDir = GetEvidenceDirectory(evidence.RunId);

        if (!Directory.Exists(evidenceDir))
        {
            Directory.CreateDirectory(evidenceDir);
        }

        var filePath = Path.Combine(evidenceDir, $"{evidence.CallId}.json");
        var json = JsonSerializer.Serialize(evidence, JsonOptions);

        // Ensure LF line endings
        var normalizedJson = json.Replace("\r\n", "\n");
        await File.WriteAllTextAsync(filePath, normalizedJson, cancellationToken);

        return filePath;
    }

    private string GetEvidenceDirectory(string runId)
    {
        var workspaceRoot = _workspace.GetAbsolutePathForArtifactId("workspace");
        return Path.Combine(workspaceRoot, ".aos", "evidence", "runs", runId, "tool-calling");
    }
}
