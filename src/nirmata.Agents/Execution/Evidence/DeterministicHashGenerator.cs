using System.Security.Cryptography;
using System.Text;

namespace nirmata.Agents.Execution.Evidence;

/// <summary>
/// Generates deterministic SHA256 hashes of execution evidence for integrity verification.
/// </summary>
public sealed class DeterministicHashGenerator
{
    /// <summary>
    /// Computes a deterministic hash from multiple evidence files.
    /// </summary>
    /// <param name="toolCallsPath">Path to tool-calls.ndjson</param>
    /// <param name="changesPatchPath">Path to changes.patch</param>
    /// <param name="executionSummaryPath">Path to execution-summary.json</param>
    /// <returns>SHA256 hash in lowercase hex format.</returns>
    public string ComputeHash(string toolCallsPath, string changesPatchPath, string executionSummaryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolCallsPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(changesPatchPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(executionSummaryPath);

        try
        {
            var toolCallsContent = File.Exists(toolCallsPath) ? File.ReadAllText(toolCallsPath) : string.Empty;
            var changesPatchContent = File.Exists(changesPatchPath) ? File.ReadAllText(changesPatchPath) : string.Empty;
            var executionSummaryContent = File.Exists(executionSummaryPath) ? File.ReadAllText(executionSummaryPath) : string.Empty;

            return ComputeHashFromContent(toolCallsContent, changesPatchContent, executionSummaryContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to compute deterministic hash", ex);
        }
    }

    /// <summary>
    /// Computes a deterministic hash from content strings.
    /// </summary>
    public string ComputeHashFromContent(string toolCallsContent, string changesPatchContent, string executionSummaryContent)
    {
        ArgumentNullException.ThrowIfNull(toolCallsContent);
        ArgumentNullException.ThrowIfNull(changesPatchContent);
        ArgumentNullException.ThrowIfNull(executionSummaryContent);

        // Concatenate in deterministic order with consistent line endings
        var combined = new StringBuilder();
        combined.Append(NormalizeContent(toolCallsContent));
        combined.Append(NormalizeContent(changesPatchContent));
        combined.Append(NormalizeContent(executionSummaryContent));

        var bytes = Encoding.UTF8.GetBytes(combined.ToString());
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Normalizes content for deterministic hashing (consistent line endings, no trailing whitespace).
    /// </summary>
    private static string NormalizeContent(string content)
    {
        if (string.IsNullOrEmpty(content))
            return string.Empty;

        // Normalize line endings to LF
        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");

        // Remove trailing whitespace from each line
        var lines = normalized.Split('\n');
        var trimmedLines = lines.Select(line => line.TrimEnd()).ToArray();

        // Rejoin with LF
        return string.Join("\n", trimmedLines);
    }

    /// <summary>
    /// Writes the hash to a file.
    /// </summary>
    public void WriteHashFile(string hashFilePath, string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hashFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);

        try
        {
            var directory = Path.GetDirectoryName(hashFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(hashFilePath, hash);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to write hash file to {hashFilePath}: {ex.Message}", ex);
        }
    }
}
