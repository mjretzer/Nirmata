namespace Gmsd.Aos.Public.Templates.Prompts;

/// <summary>
/// Interface for loading prompt templates by ID.
/// </summary>
public interface IPromptTemplateLoader
{
    /// <summary>
    /// Gets a prompt template by its identifier.
    /// </summary>
    /// <param name="id">The template identifier (e.g., "planning.task-breakdown.v1").</param>
    /// <returns>The prompt template if found; otherwise, null.</returns>
    PromptTemplate? GetById(string id);

    /// <summary>
    /// Checks if a template with the given ID exists.
    /// </summary>
    bool Exists(string id);
}
