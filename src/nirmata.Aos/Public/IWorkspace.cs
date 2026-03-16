namespace nirmata.Aos.Public;

using System.Text.Json;

/// <summary>
/// Public AOS workspace abstraction (compile-time contract).
/// </summary>
public interface IWorkspace
{
    string RepositoryRootPath { get; }
    string AosRootPath { get; }

    /// <summary>
    /// Resolves a supported artifact id to its canonical contract path under <c>.aos/*</c>.
    /// </summary>
    string GetContractPathForArtifactId(string artifactId);

    /// <summary>
    /// Resolves a canonical contract path under <c>.aos/*</c> to an absolute filesystem path safely.
    /// </summary>
    string GetAbsolutePathForContractPath(string contractPath);

    /// <summary>
    /// Resolves a supported artifact id directly to an absolute filesystem path.
    /// </summary>
    string GetAbsolutePathForArtifactId(string artifactId);

    /// <summary>
    /// Reads a JSON artifact from the specified subpath and filename under the AOS root.
    /// </summary>
    JsonElement ReadArtifact(string subpath, string filename);
}

