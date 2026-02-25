using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Specs;

public class ProjectModel : PageModel
{
    private readonly ILogger<ProjectModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ProjectModel(ILogger<ProjectModel> logger, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    // Form properties
    [BindProperty]
    public string ProjectName { get; set; } = string.Empty;

    [BindProperty]
    public string ProjectDescription { get; set; } = string.Empty;

    [BindProperty]
    public string Constraints { get; set; } = string.Empty;

    [BindProperty]
    public string SuccessCriteria { get; set; } = string.Empty;

    [BindProperty]
    public string? RawContent { get; set; }

    [BindProperty(SupportsGet = true)]
    public string EditMode { get; set; } = "form";

    // Display properties
    public string? WorkspacePath { get; set; }
    public string? ProjectFilePath { get; set; }
    public string? SchemaFilePath { get; set; }
    public string? DiskPath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public string? DraftMessage { get; set; }
    public bool IsValid { get; set; } = true;
    public List<ValidationError> ValidationErrors { get; set; } = new();

    public void OnGet()
    {
        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath))
        {
            ErrorMessage = "No workspace selected. Please select a workspace first.";
            return;
        }

        ProjectFilePath = Path.Combine(WorkspacePath, ".aos", "spec", "project.json");
        SchemaFilePath = Path.Combine(WorkspacePath, ".aos", "schemas", "project.schema.json");
        DiskPath = $"file://{ProjectFilePath}";

        if (!System.IO.File.Exists(ProjectFilePath))
        {
            // Create default project.json if it doesn't exist
            var defaultProject = new ProjectSpec
            {
                SchemaVersion = 1,
                Project = new ProjectDetails
                {
                    Name = "",
                    Description = ""
                }
            };
            
            var dir = Path.GetDirectoryName(ProjectFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            
            System.IO.File.WriteAllText(ProjectFilePath, 
                JsonSerializer.Serialize(defaultProject, new JsonSerializerOptions { WriteIndented = true }));
        }

        try
        {
            var content = System.IO.File.ReadAllText(ProjectFilePath);
            RawContent = content;

            // Parse existing project.json
            var project = JsonSerializer.Deserialize<ProjectSpec>(content);
            if (project?.Project != null)
            {
                ProjectName = project.Project.Name ?? "";
                ProjectDescription = project.Project.Description ?? "";
                
                // Handle extended fields (constraints and success criteria)
                if (project.Project.Constraints is JsonElement constraintsElement)
                {
                    if (constraintsElement.ValueKind == JsonValueKind.Array)
                    {
                        Constraints = string.Join("\n", constraintsElement.EnumerateArray()
                            .Select(c => c.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)));
                    }
                }
                
                if (project.Project.SuccessCriteria is JsonElement criteriaElement)
                {
                    if (criteriaElement.ValueKind == JsonValueKind.Array)
                    {
                        SuccessCriteria = string.Join("\n", criteriaElement.EnumerateArray()
                            .Select(c => c.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)));
                    }
                }
            }

            // Validate against schema
            ValidateProject(content);
        }
        catch (JsonException ex)
        {
            ErrorMessage = $"Invalid JSON in project.json: {ex.Message}";
            IsValid = false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading project.json");
            ErrorMessage = $"Error loading project: {ex.Message}";
        }

        // Read TempData messages
        if (TempData["SuccessMessage"] != null)
        {
            SuccessMessage = TempData["SuccessMessage"]?.ToString();
        }
        if (TempData["ErrorMessage"] != null)
        {
            ErrorMessage = TempData["ErrorMessage"]?.ToString();
        }
    }

    public IActionResult OnPost()
    {
        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath))
        {
            TempData["ErrorMessage"] = "No workspace selected.";
            return RedirectToPage();
        }

        ProjectFilePath = Path.Combine(WorkspacePath, ".aos", "spec", "project.json");

        try
        {
            string content;
            ProjectSpec project;

            if (EditMode == "form")
            {
                // Build project from form fields
                var constraintsList = Constraints?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList() ?? new List<string>();

                var successCriteriaList = SuccessCriteria?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList() ?? new List<string>();

                project = new ProjectSpec
                {
                    SchemaVersion = 1,
                    Project = new ProjectDetails
                    {
                        Name = ProjectName?.Trim() ?? "",
                        Description = ProjectDescription?.Trim() ?? ""
                    }
                };

                // Add extended fields if they have values
                var projectDict = new Dictionary<string, object>
                {
                    ["name"] = project.Project.Name,
                    ["description"] = project.Project.Description
                };

                if (constraintsList.Any())
                {
                    projectDict["constraints"] = constraintsList;
                }

                if (successCriteriaList.Any())
                {
                    projectDict["successCriteria"] = successCriteriaList;
                }

                // Serialize with extended properties
                var options = new JsonSerializerOptions { WriteIndented = true };
                var root = new Dictionary<string, object>
                {
                    ["schemaVersion"] = 1,
                    ["project"] = projectDict
                };

                content = JsonSerializer.Serialize(root, options);
            }
            else
            {
                // Use raw content
                content = RawContent ?? "{}";
                
                // Validate JSON structure
                try
                {
                    var parsedProject = JsonSerializer.Deserialize<ProjectSpec>(content);
                    if (parsedProject == null)
                    {
                        TempData["ErrorMessage"] = "Failed to parse project JSON.";
                        return RedirectToPage(new { editMode = EditMode });
                    }
                }
                catch (JsonException ex)
                {
                    TempData["ErrorMessage"] = $"Invalid JSON: {ex.Message}";
                    return RedirectToPage(new { editMode = EditMode });
                }
            }

            // Validate against schema
            var validationResult = ValidateProject(content);
            if (!validationResult.IsValid)
            {
                TempData["ErrorMessage"] = "Validation failed. Please fix the errors and try again.";
                return RedirectToPage(new { editMode = EditMode });
            }

            // Write to file
            System.IO.File.WriteAllText(ProjectFilePath, content);

            // Clear draft from localStorage (will be done client-side)
            TempData["SuccessMessage"] = "Project spec saved successfully!";

            return RedirectToPage(new { editMode = EditMode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving project.json");
            TempData["ErrorMessage"] = $"Error saving project: {ex.Message}";
            return RedirectToPage(new { editMode = EditMode });
        }
    }

    public IActionResult OnPostValidate([FromForm] string projectName, [FromForm] string projectDescription,
        [FromForm] string constraints, [FromForm] string successCriteria)
    {
        LoadWorkspace();

        var constraintsList = constraints?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList() ?? new List<string>();

        var successCriteriaList = successCriteria?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList() ?? new List<string>();

        var projectDict = new Dictionary<string, object>
        {
            ["name"] = projectName?.Trim() ?? "",
            ["description"] = projectDescription?.Trim() ?? ""
        };

        if (constraintsList.Any())
        {
            projectDict["constraints"] = constraintsList;
        }

        if (successCriteriaList.Any())
        {
            projectDict["successCriteria"] = successCriteriaList;
        }

        var root = new Dictionary<string, object>
        {
            ["schemaVersion"] = 1,
            ["project"] = projectDict
        };

        var content = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        var result = ValidateProject(content);

        return new JsonResult(new { valid = result.IsValid, errors = result.Errors.Select(e => $"{e.Path}: {e.Message}").ToList() });
    }

    public IActionResult OnPostValidateRaw([FromBody] ValidateRequest request)
    {
        if (request?.Content == null)
        {
            return new JsonResult(new { valid = false, errors = new[] { "No content provided" } });
        }

        var result = ValidateProject(request.Content);
        return new JsonResult(new { valid = result.IsValid, errors = result.Errors.Select(e => $"{e.Path}: {e.Message}").ToList() });
    }

    private ValidationResult ValidateProject(string content)
    {
        var result = new ValidationResult();

        try
        {
            // First, validate JSON is parseable
            var doc = JsonSerializer.Deserialize<JsonElement>(content);

            // Check required fields
            if (!doc.TryGetProperty("schemaVersion", out _))
            {
                result.AddError("schemaVersion", "Missing required field: schemaVersion");
            }

            if (!doc.TryGetProperty("project", out var projectElement))
            {
                result.AddError("project", "Missing required field: project");
            }
            else
            {
                if (!projectElement.TryGetProperty("name", out var nameElement) || 
                    string.IsNullOrWhiteSpace(nameElement.GetString()))
                {
                    result.AddError("project.name", "Project name is required");
                }

                if (!projectElement.TryGetProperty("description", out var descElement) || 
                    string.IsNullOrWhiteSpace(descElement.GetString()))
                {
                    result.AddError("project.description", "Project description is required");
                }
            }

            // If schema file exists, perform additional validation
            SchemaFilePath = Path.Combine(WorkspacePath ?? "", ".aos", "schemas", "project.schema.json");
            if (System.IO.File.Exists(SchemaFilePath))
            {
                try
                {
                    var schemaContent = System.IO.File.ReadAllText(SchemaFilePath);
                    var schema = JsonSerializer.Deserialize<JsonElement>(schemaContent);
                    
                    // Basic schema type checking
                    ValidateAgainstSchema(doc, schema, "", result);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not validate against schema file");
                }
            }
        }
        catch (JsonException ex)
        {
            result.AddError("", $"Invalid JSON: {ex.Message}");
        }

        IsValid = result.IsValid;
        ValidationErrors = result.Errors;

        return result;
    }

    private void ValidateAgainstSchema(JsonElement data, JsonElement schema, string path, ValidationResult result)
    {
        // Basic schema validation - check required fields exist
        if (schema.TryGetProperty("required", out var required))
        {
            foreach (var req in required.EnumerateArray())
            {
                var reqName = req.GetString();
                if (!string.IsNullOrEmpty(reqName) && !data.TryGetProperty(reqName, out _))
                {
                    result.AddError(string.IsNullOrEmpty(path) ? reqName : $"{path}.{reqName}", 
                        $"Missing required field: {reqName}");
                }
            }
        }

        // Check nested properties
        if (schema.TryGetProperty("properties", out var properties))
        {
            foreach (var prop in properties.EnumerateObject())
            {
                if (data.TryGetProperty(prop.Name, out var propValue))
                {
                    var propSchema = prop.Value;
                    var propPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";

                    // Check type
                    if (propSchema.TryGetProperty("type", out var typeElement))
                    {
                        var expectedType = typeElement.GetString();
                        var actualType = propValue.ValueKind;
                        
                        if (!IsTypeMatch(expectedType, actualType))
                        {
                            result.AddError(propPath, $"Expected type {expectedType}, got {actualType}");
                        }
                    }

                    // Recurse into objects
                    if (propValue.ValueKind == JsonValueKind.Object && 
                        propSchema.TryGetProperty("properties", out _))
                    {
                        ValidateAgainstSchema(propValue, propSchema, propPath, result);
                    }
                }
            }
        }
    }

    private static bool IsTypeMatch(string? expectedType, JsonValueKind actualKind)
    {
        return expectedType?.ToLower() switch
        {
            "string" => actualKind == JsonValueKind.String,
            "integer" => actualKind == JsonValueKind.Number,
            "number" => actualKind == JsonValueKind.Number,
            "boolean" => actualKind == JsonValueKind.True || actualKind == JsonValueKind.False,
            "object" => actualKind == JsonValueKind.Object,
            "array" => actualKind == JsonValueKind.Array,
            _ => true
        };
    }

    private void LoadWorkspace()
    {
        var workspaceFile = Path.Combine(_environment.ContentRootPath, "sqllitedb", "workspace.txt");
        if (System.IO.File.Exists(workspaceFile))
        {
            WorkspacePath = System.IO.File.ReadAllText(workspaceFile).Trim();
        }
        else
        {
            WorkspacePath = _configuration["Aos:WorkspacePath"];
        }
    }

    public class ValidationError
    {
        public string Path { get; set; } = "";
        public string Message { get; set; } = "";
    }

    private class ValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<ValidationError> Errors { get; } = new();

        public void AddError(string path, string message)
        {
            Errors.Add(new ValidationError { Path = path, Message = message });
        }
    }

    public IActionResult OnPostImport(IFormFile importFile)
    {
        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return new JsonResult(new { success = false, error = "No workspace selected." });
        }

        if (importFile == null || importFile.Length == 0)
        {
            return new JsonResult(new { success = false, error = "No file uploaded." });
        }

        try
        {
            using var reader = new StreamReader(importFile.OpenReadStream());
            var content = reader.ReadToEnd();
            
            // Validate it's valid JSON and matches project schema
            var doc = JsonSerializer.Deserialize<JsonElement>(content);
            
            if (!doc.TryGetProperty("project", out var projectElement))
            {
                return new JsonResult(new { success = false, error = "Invalid project file: missing 'project' section." });
            }

            // Extract data for response
            var result = new Dictionary<string, object>();
            
            if (projectElement.TryGetProperty("name", out var nameElement))
            {
                result["name"] = nameElement.GetString() ?? "";
            }
            
            if (projectElement.TryGetProperty("description", out var descElement))
            {
                result["description"] = descElement.GetString() ?? "";
            }
            
            if (projectElement.TryGetProperty("constraints", out var constraintsElement) && 
                constraintsElement.ValueKind == JsonValueKind.Array)
            {
                result["constraints"] = string.Join("\n", constraintsElement.EnumerateArray()
                    .Select(c => c.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)));
            }
            
            if (projectElement.TryGetProperty("successCriteria", out var criteriaElement) && 
                criteriaElement.ValueKind == JsonValueKind.Array)
            {
                result["successCriteria"] = string.Join("\n", criteriaElement.EnumerateArray()
                    .Select(c => c.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)));
            }
            
            result["rawContent"] = content;
            result["success"] = true;
            
            return new JsonResult(result);
        }
        catch (JsonException ex)
        {
            return new JsonResult(new { success = false, error = $"Invalid JSON: {ex.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing project file");
            return new JsonResult(new { success = false, error = $"Import failed: {ex.Message}" });
        }
    }

    public IActionResult OnPostExport([FromBody] ExportRequest? request)
    {
        LoadWorkspace();

        try
        {
            string content;
            string fileName;
            
            if (request?.RawContent != null)
            {
                // Export raw JSON mode content
                content = request.RawContent;
                fileName = "project.json";
            }
            else if (request?.ProjectName != null)
            {
                // Build from form data
                var constraintsList = request.Constraints?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList() ?? new List<string>();

                var successCriteriaList = request.SuccessCriteria?.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList() ?? new List<string>();

                var projectDict = new Dictionary<string, object>
                {
                    ["name"] = request.ProjectName.Trim(),
                    ["description"] = request.ProjectDescription?.Trim() ?? ""
                };

                if (constraintsList.Any())
                {
                    projectDict["constraints"] = constraintsList;
                }

                if (successCriteriaList.Any())
                {
                    projectDict["successCriteria"] = successCriteriaList;
                }

                var root = new Dictionary<string, object>
                {
                    ["schemaVersion"] = 1,
                    ["project"] = projectDict,
                    ["exportedAt"] = DateTime.UtcNow.ToString("O")
                };

                content = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
                fileName = $"{request.ProjectName.Replace(' ', '_').ToLowerInvariant()}_project.json";
            }
            else
            {
                return new JsonResult(new { success = false, error = "No content to export." });
            }

            return new JsonResult(new 
            { 
                success = true, 
                content = content, 
                fileName = fileName 
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting project");
            return new JsonResult(new { success = false, error = $"Export failed: {ex.Message}" });
        }
    }

    public class ExportRequest
    {
        [JsonPropertyName("projectName")]
        public string? ProjectName { get; set; }
        
        [JsonPropertyName("projectDescription")]
        public string? ProjectDescription { get; set; }
        
        [JsonPropertyName("constraints")]
        public string? Constraints { get; set; }
        
        [JsonPropertyName("successCriteria")]
        public string? SuccessCriteria { get; set; }
        
        [JsonPropertyName("rawContent")]
        public string? RawContent { get; set; }
    }

    private class ProjectSpec
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("project")]
        public ProjectDetails Project { get; set; } = new();
    }

    private class ProjectDetails
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }

        public object? Constraints { get; set; }
        public object? SuccessCriteria { get; set; }
    }

    public class ValidateRequest
    {
        [JsonPropertyName("content")]
        public string Content { get; set; } = "";
    }
}
