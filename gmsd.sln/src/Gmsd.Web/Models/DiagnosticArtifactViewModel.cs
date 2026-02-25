namespace Gmsd.Web.Models;

public class ValidationErrorViewModel
{
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Expected { get; set; }
    public string? Actual { get; set; }
}

public class DiagnosticArtifactViewModel
{
    public int SchemaVersion { get; set; } = 1;
    public string SchemaId { get; set; } = "gmsd:aos:schema:diagnostic:v1";
    public string ArtifactPath { get; set; } = string.Empty;
    public string FailedSchemaId { get; set; } = string.Empty;
    public int FailedSchemaVersion { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string Phase { get; set; } = string.Empty;
    public Dictionary<string, string> Context { get; set; } = new();
    public List<ValidationErrorViewModel> ValidationErrors { get; set; } = new();
    public List<string> RepairSuggestions { get; set; } = new();

    public bool HasErrors => ValidationErrors.Count > 0;
    public bool HasSuggestions => RepairSuggestions.Count > 0;
    public string ErrorCount => ValidationErrors.Count.ToString();
    public string SuggestionCount => RepairSuggestions.Count.ToString();
}

public class ArtifactValidationStatusViewModel
{
    public bool IsValid { get; set; } = true;
    public string? ValidationMessage { get; set; }
    public DiagnosticArtifactViewModel? Diagnostic { get; set; }
    public DateTime? ValidatedAt { get; set; }
}
