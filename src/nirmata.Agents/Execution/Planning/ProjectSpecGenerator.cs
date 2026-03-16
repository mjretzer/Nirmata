using System.Text.Json;
using System.Text.Json.Serialization;

namespace nirmata.Agents.Execution.Planning;

/// <summary>
/// Generates normalized project specifications from interview session data.
/// </summary>
public interface IProjectSpecGenerator
{
    /// <summary>
    /// Generates a project specification from the interview session.
    /// </summary>
    /// <param name="session">The completed interview session.</param>
    /// <returns>The normalized project specification.</returns>
    ProjectSpecification GenerateFromSession(InterviewSession session);

    /// <summary>
    /// Serializes the project specification to JSON with deterministic formatting.
    /// </summary>
    /// <param name="spec">The project specification.</param>
    /// <returns>JSON string with stable key ordering and LF endings.</returns>
    string SerializeToJson(ProjectSpecification spec);

    /// <summary>
    /// Parses a project specification from JSON.
    /// </summary>
    /// <param name="json">The JSON string.</param>
    /// <returns>The parsed project specification.</returns>
    ProjectSpecification? ParseFromJson(string json);

    /// <summary>
    /// Validates that the specification conforms to the expected schema.
    /// </summary>
    /// <param name="spec">The project specification to validate.</param>
    /// <returns>Validation result with errors if any.</returns>
    SpecValidationResult Validate(ProjectSpecification spec);
}

/// <summary>
/// Result of validating a project specification.
/// </summary>
public sealed record SpecValidationResult
{
    /// <summary>
    /// Whether the specification is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// List of validation errors if any.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static SpecValidationResult Success() => new() { IsValid = true };

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static SpecValidationResult Failure(IEnumerable<string> errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}

/// <summary>
/// Default implementation of the project specification generator.
/// </summary>
public sealed class ProjectSpecGenerator : IProjectSpecGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    /// <inheritdoc />
    public ProjectSpecification GenerateFromSession(InterviewSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var draft = session.ProjectDraft;
        if (draft == null)
        {
            throw new InvalidOperationException("Cannot generate spec from session without a project draft.");
        }

        return new ProjectSpecification
        {
            Schema = "nirmata:aos:schema:project:v1",
            Name = draft.Name ?? "Untitled Project",
            Description = draft.Description ?? "No description provided.",
            TechnologyStack = draft.TechnologyStack,
            Goals = draft.Goals.AsReadOnly(),
            TargetAudience = draft.TargetAudience,
            KeyFeatures = draft.KeyFeatures.AsReadOnly(),
            Constraints = draft.Constraints.AsReadOnly(),
            Assumptions = draft.Assumptions.AsReadOnly(),
            Metadata = new Dictionary<string, object>(draft.Metadata)
            {
                ["generatedFromInterview"] = true,
                ["interviewSessionId"] = session.SessionId,
                ["interviewCompletedAt"] = session.CompletedAt?.ToString("O") ?? DateTimeOffset.UtcNow.ToString("O"),
                ["qaPairCount"] = session.QAPairs.Count
            }
        };
    }

    /// <inheritdoc />
    public string SerializeToJson(ProjectSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        // Create schema-compliant structure matching project.schema.json
        var schemaCompliantSpec = new
        {
            schemaVersion = 1,
            project = new
            {
                name = spec.Name,
                description = spec.Description
            }
        };

        var json = JsonSerializer.Serialize(schemaCompliantSpec, JsonOptions);
        // Ensure LF line endings for deterministic output
        return json.Replace("\r\n", "\n");
    }

    /// <inheritdoc />
    public ProjectSpecification? ParseFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Handle nested schema format: { schemaVersion, project: { name, description } }
            if (root.TryGetProperty("project", out var projectElement))
            {
                var name = projectElement.GetProperty("name").GetString() ?? "";
                var description = projectElement.GetProperty("description").GetString() ?? "";

                return new ProjectSpecification
                {
                    Schema = "nirmata:aos:schema:project:v1",
                    Name = name,
                    Description = description
                };
            }

            // Fallback: try direct deserialization for flat format
            return JsonSerializer.Deserialize<ProjectSpecification>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public SpecValidationResult Validate(ProjectSpecification spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var errors = new List<string>();

        // Check required schema
        if (string.IsNullOrWhiteSpace(spec.Schema))
        {
            errors.Add("Schema is required.");
        }
        else if (!spec.Schema.Equals("nirmata:aos:schema:project:v1", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Unexpected schema: {spec.Schema}. Expected: nirmata:aos:schema:project:v1");
        }

        // Check required fields
        if (string.IsNullOrWhiteSpace(spec.Name))
        {
            errors.Add("Project name is required.");
        }

        if (string.IsNullOrWhiteSpace(spec.Description))
        {
            errors.Add("Project description is required.");
        }

        return errors.Count == 0
            ? SpecValidationResult.Success()
            : SpecValidationResult.Failure(errors);
    }
}
