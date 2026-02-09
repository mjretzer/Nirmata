namespace Gmsd.Web.Models;

public class ValidationReportModel
{
    public bool IsValid { get; set; }
    public List<ValidationIssue> Errors { get; set; } = new();
    public List<ValidationIssue> Warnings { get; set; } = new();
    public string? SchemaName { get; set; }

    public string GetStatusClass()
    {
        return IsValid ? "validation-success" : "validation-error";
    }
}

public class ValidationIssue
{
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Suggestion { get; set; }
}
