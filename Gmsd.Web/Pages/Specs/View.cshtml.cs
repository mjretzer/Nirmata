using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Specs;

public class ViewModel : PageModel
{
    private readonly ILogger<ViewModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public ViewModel(ILogger<ViewModel> logger, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _logger = logger;
        _configuration = configuration;
        _environment = environment;
    }

    [BindProperty(SupportsGet = true)]
    public string? Path { get; set; }

    [BindProperty(SupportsGet = true)]
    public string EditMode { get; set; } = "raw";

    public string? WorkspacePath { get; set; }
    public string? FilePath { get; set; }
    public string? FileName { get; set; }
    public string? DiskPath { get; set; }
    public string? RawContent { get; set; }
    public bool IsJson { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public List<ValidationError> ValidationErrors { get; set; } = new();
    public List<FormField> FormFields { get; set; } = new();

    // Diff viewer properties
    public bool ShowDiff { get; set; }
    public List<GitCommitInfo> GitHistory { get; set; } = new();
    public string? SelectedCommit { get; set; }
    public List<DiffLine> DiffLines { get; set; } = new();

    public void OnGet()
    {
        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(Path))
        {
            ErrorMessage = "Invalid file path or workspace.";
            return;
        }

        FilePath = Path;
        FileName = System.IO.Path.GetFileName(Path);
        DiskPath = $"file://{GetFullPath()}";

        var fullPath = GetFullPath();
        if (!System.IO.File.Exists(fullPath))
        {
            ErrorMessage = "File not found.";
            return;
        }

        try
        {
            RawContent = System.IO.File.ReadAllText(fullPath);
            IsJson = System.IO.Path.GetExtension(fullPath).Equals(".json", StringComparison.OrdinalIgnoreCase);

            if (IsJson)
            {
                // Try to parse as JSON
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(RawContent);
                
                // Build form fields from JSON schema if available
                FormFields = BuildFormFields(jsonDoc);
                
                // Validate against schema
                ValidateAgainstSchema(RawContent, Path);
            }
        }
        catch (JsonException ex)
        {
            ErrorMessage = $"Invalid JSON: {ex.Message}";
            IsJson = true; // Still treat as JSON for editing purposes
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading file: {Path}", Path);
            ErrorMessage = $"Error loading file: {ex.Message}";
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

        // Load git history for diff viewer
        LoadGitHistory();
    }

    public IActionResult OnPost(string path, string editMode, Dictionary<string, string> formData, string? rawContent = null)
    {
        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(path))
        {
            TempData["ErrorMessage"] = "Invalid file path or workspace.";
            return RedirectToPage(new { path });
        }

        FilePath = path;
        var fullPath = GetFullPath();

        // Security check
        var specPath = System.IO.Path.Combine(WorkspacePath, ".aos", "spec");
        if (!fullPath.StartsWith(specPath, StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Invalid file path.";
            return RedirectToPage(new { path });
        }

        try
        {
            string content;
            bool isJson = System.IO.Path.GetExtension(fullPath).Equals(".json", StringComparison.OrdinalIgnoreCase);

            if (isJson && editMode == "form" && formData != null && formData.Any())
            {
                // Reconstruct JSON from form data
                var jsonDoc = ReconstructJsonFromForm(formData);
                content = JsonSerializer.Serialize(jsonDoc, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                content = rawContent ?? string.Empty;
            }

            // Validate JSON content
            if (isJson)
            {
                try
                {
                    JsonSerializer.Deserialize<JsonElement>(content);
                }
                catch (JsonException ex)
                {
                    TempData["ErrorMessage"] = $"Invalid JSON: {ex.Message}";
                    return RedirectToPage(new { path, editMode });
                }

                // Validate against schema
                var validationErrors = ValidateAgainstSchema(content, path);
                if (validationErrors.Any())
                {
                    TempData["ErrorMessage"] = "Schema validation failed. Please fix the errors.";
                    return RedirectToPage(new { path, editMode });
                }
            }

            // Backup existing file
            if (System.IO.File.Exists(fullPath))
            {
                var backupPath = fullPath + ".backup";
                System.IO.File.Copy(fullPath, backupPath, true);
            }

            System.IO.File.WriteAllText(fullPath, content);
            TempData["SuccessMessage"] = "File saved successfully.";

            _logger.LogInformation("Saved spec file: {FilePath}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save spec file: {FilePath}", path);
            TempData["ErrorMessage"] = $"Failed to save file: {ex.Message}";
        }

        return RedirectToPage(new { path, editMode });
    }

    public IActionResult OnGetDiff(string path, string? commit = null)
    {
        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(path))
        {
            return new JsonResult(new { error = "Invalid path or workspace" });
        }

        FilePath = path;
        var fullPath = GetFullPath();

        if (!System.IO.File.Exists(fullPath))
        {
            return new JsonResult(new { error = "File not found" });
        }

        try
        {
            var currentContent = System.IO.File.ReadAllText(fullPath);
            string? previousContent = null;

            if (!string.IsNullOrEmpty(commit))
            {
                // Get file content at specific commit
                previousContent = GetFileContentAtCommit(fullPath, commit);
            }
            else
            {
                // Get content from HEAD~1 (previous commit)
                previousContent = GetFileContentAtCommit(fullPath, "HEAD");
            }

            var diffLines = ComputeDiff(previousContent ?? "", currentContent);

            return new JsonResult(new
            {
                diff = diffLines,
                commit = commit ?? "HEAD",
                previousContent = previousContent ?? "",
                currentContent
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing diff for file: {Path}", path);
            return new JsonResult(new { error = ex.Message });
        }
    }

    private void LoadGitHistory()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
            return;

        try
        {
            var fullPath = GetFullPath();
            if (!System.IO.File.Exists(fullPath))
                return;

            GitHistory = GetGitHistoryForFile(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load git history for file");
        }
    }

    private List<GitCommitInfo> GetGitHistoryForFile(string filePath)
    {
        var commits = new List<GitCommitInfo>();

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"log --format=\"%H|%ci|%s|%an\" -- \"{filePath}\"",
                WorkingDirectory = WorkspacePath!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return commits;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return commits;

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var parts = line.Split('|', 4);
                if (parts.Length >= 4)
                {
                    commits.Add(new GitCommitInfo
                    {
                        Hash = parts[0],
                        Date = DateTime.TryParse(parts[1], out var dt) ? dt : DateTime.MinValue,
                        Message = parts[2],
                        Author = parts[3]
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git log command failed");
        }

        return commits;
    }

    private string? GetFileContentAtCommit(string filePath, string commit)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"show {commit}:\"{filePath.Replace(WorkspacePath! + System.IO.Path.DirectorySeparatorChar, "")}\"",
                WorkingDirectory = WorkspacePath!,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                return null;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Git show command failed for commit {Commit}", commit);
            return null;
        }
    }

    private List<DiffLine> ComputeDiff(string oldContent, string newContent)
    {
        var diffLines = new List<DiffLine>();
        var oldLines = oldContent.Split('\n');
        var newLines = newContent.Split('\n');

        // Simple line-by-line diff
        int maxLines = Math.Max(oldLines.Length, newLines.Length);
        int oldIndex = 0, newIndex = 0;

        while (oldIndex < oldLines.Length || newIndex < newLines.Length)
        {
            if (oldIndex < oldLines.Length && newIndex < newLines.Length)
            {
                if (oldLines[oldIndex] == newLines[newIndex])
                {
                    diffLines.Add(new DiffLine
                    {
                        Type = DiffLineType.Unchanged,
                        OldLineNumber = oldIndex + 1,
                        NewLineNumber = newIndex + 1,
                        Content = oldLines[oldIndex]
                    });
                    oldIndex++;
                    newIndex++;
                }
                else
                {
                    // Check if this is a modified line
                    diffLines.Add(new DiffLine
                    {
                        Type = DiffLineType.Removed,
                        OldLineNumber = oldIndex + 1,
                        NewLineNumber = null,
                        Content = oldLines[oldIndex]
                    });
                    diffLines.Add(new DiffLine
                    {
                        Type = DiffLineType.Added,
                        OldLineNumber = null,
                        NewLineNumber = newIndex + 1,
                        Content = newLines[newIndex]
                    });
                    oldIndex++;
                    newIndex++;
                }
            }
            else if (oldIndex < oldLines.Length)
            {
                diffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Removed,
                    OldLineNumber = oldIndex + 1,
                    NewLineNumber = null,
                    Content = oldLines[oldIndex]
                });
                oldIndex++;
            }
            else
            {
                diffLines.Add(new DiffLine
                {
                    Type = DiffLineType.Added,
                    OldLineNumber = null,
                    NewLineNumber = newIndex + 1,
                    Content = newLines[newIndex]
                });
                newIndex++;
            }
        }

        return diffLines;
    }

    public IActionResult OnPostValidate([FromBody] ValidationRequest request)
    {
        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(request.Path))
        {
            return new JsonResult(new { valid = false, errors = new[] { "Invalid path or workspace" } });
        }

        try
        {
            // First, check if it's valid JSON
            try
            {
                JsonSerializer.Deserialize<JsonElement>(request.Content);
            }
            catch (JsonException ex)
            {
                return new JsonResult(new { valid = false, errors = new[] { $"Invalid JSON: {ex.Message}" } });
            }

            // Then validate against schema
            var errors = ValidateAgainstSchema(request.Content, request.Path);

            if (errors.Any())
            {
                return new JsonResult(new {
                    valid = false,
                    errors = errors.Select(e => $"{e.Path}: {e.Message}").ToArray()
                });
            }

            return new JsonResult(new { valid = true, errors = Array.Empty<string>() });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { valid = false, errors = new[] { ex.Message } });
        }
    }

    private string GetFullPath()
    {
        if (string.IsNullOrEmpty(WorkspacePath) || string.IsNullOrEmpty(FilePath))
            return string.Empty;

        var safePath = FilePath.Replace("/", System.IO.Path.DirectorySeparatorChar.ToString());
        return System.IO.Path.Combine(WorkspacePath, ".aos", "spec", safePath);
    }

    private void LoadWorkspace()
    {
        try
        {
            var configPath = GetWorkspaceConfigPath();
            if (System.IO.File.Exists(configPath))
            {
                var json = System.IO.File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<WorkspaceConfig>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                WorkspacePath = config?.SelectedWorkspacePath;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load workspace configuration");
        }
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return System.IO.Path.Combine(appData, "Gmsd", "workspace-config.json");
    }

    private List<FormField> BuildFormFields(JsonElement jsonDoc)
    {
        var fields = new List<FormField>();

        // Get schema for this file type to determine field types
        var schema = GetSchemaForFile(FilePath);

        foreach (var property in jsonDoc.EnumerateObject())
        {
            JsonElement? propertySchema = null;
            if (schema.HasValue && schema.Value.TryGetProperty("properties", out var props))
            {
                if (props.TryGetProperty(property.Name, out var prop))
                {
                    propertySchema = prop;
                }
            }

            var field = new FormField
            {
                Key = property.Name,
                DisplayName = GetDisplayName(property.Name),
                Value = GetJsonValue(property.Value),
                Type = GetFieldType(property.Value, propertySchema),
                IsRequired = IsRequiredField(property.Name, schema),
                Description = GetFieldDescription(property.Name, schema)
            };
            fields.Add(field);
        }

        return fields;
    }

    private JsonElement? GetSchemaForFile(string? filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return null;

        // Determine schema based on file path
        var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
        var parentDir = System.IO.Path.GetDirectoryName(filePath)?.ToLowerInvariant() ?? "";

        string? schemaName = fileName switch
        {
            "project" => "project",
            "roadmap" => "roadmap",
            _ when parentDir.Contains("milestone") || fileName.StartsWith("milestone-") => "milestone",
            _ when parentDir.Contains("phase") || fileName.StartsWith("phase-") => "phase",
            _ when parentDir.Contains("task") || fileName.StartsWith("task-") => "task",
            _ when parentDir.Contains("issue") || fileName.StartsWith("issue-") => "issue",
            _ when parentDir.Contains("uat") => "uat",
            _ => null
        };

        if (string.IsNullOrEmpty(schemaName))
            return null;

        return LoadEmbeddedSchema(schemaName);
    }

    private JsonElement? LoadEmbeddedSchema(string schemaName)
    {
        try
        {
            var schemaPath = System.IO.Path.Combine(
                _environment.ContentRootPath,
                "..", "Gmsd.Aos", "Resources", "Schemas", $"{schemaName}.schema.json"
            );

            // Also check in the web project directly
            if (!System.IO.File.Exists(schemaPath))
            {
                schemaPath = System.IO.Path.Combine(
                    _environment.WebRootPath, "schemas", $"{schemaName}.schema.json"
                );
            }

            if (!System.IO.File.Exists(schemaPath))
            {
                // Try loading from Aos assembly resources
                return LoadSchemaFromAosAssembly(schemaName);
            }

            var schemaContent = System.IO.File.ReadAllText(schemaPath);
            return JsonSerializer.Deserialize<JsonElement>(schemaContent);
        }
        catch
        {
            return null;
        }
    }

    private JsonElement? LoadSchemaFromAosAssembly(string schemaName)
    {
        try
        {
            // Load from Gmsd.Aos embedded resources
            var assembly = typeof(Gmsd.Aos.Contracts.AosContracts).Assembly;
            var resourceName = $"Gmsd.Aos.Resources.Schemas.{schemaName}.schema.json";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            return JsonSerializer.Deserialize<JsonElement>(content);
        }
        catch
        {
            return null;
        }
    }

    private List<ValidationError> ValidateAgainstSchema(string content, string? filePath)
    {
        var errors = new List<ValidationError>();

        try
        {
            var jsonDoc = JsonSerializer.Deserialize<JsonElement>(content);
            var schema = GetSchemaForFile(filePath);

            if (schema.HasValue)
            {
                ValidateAgainstSchemaRecursive(jsonDoc, schema.Value, "", errors);
            }
        }
        catch (JsonException ex)
        {
            errors.Add(new ValidationError { Path = "", Message = $"Invalid JSON: {ex.Message}" });
        }

        ValidationErrors = errors;
        return errors;
    }

    private void ValidateAgainstSchemaRecursive(JsonElement instance, JsonElement schema, string path, List<ValidationError> errors)
    {
        // Basic schema validation implementation
        var schemaType = schema.GetProperty("type").GetString();
        
        if (schemaType == "object" && instance.ValueKind == JsonValueKind.Object)
        {
            // Check required properties
            if (schema.TryGetProperty("required", out var required))
            {
                foreach (var req in required.EnumerateArray())
                {
                    var reqName = req.GetString();
                    if (!instance.GetProperty(reqName!).ValueKind.Equals(null))
                    {
                        // Property exists, continue validation
                    }
                    else if (!instance.EnumerateObject().Any(p => p.Name == reqName))
                    {
                        errors.Add(new ValidationError { Path = path, Message = $"Required property '{reqName}' is missing" });
                    }
                }
            }

            // Validate properties
            if (schema.TryGetProperty("properties", out var properties))
            {
                foreach (var prop in instance.EnumerateObject())
                {
                    if (properties.TryGetProperty(prop.Name, out var propSchema))
                    {
                        var propPath = string.IsNullOrEmpty(path) ? prop.Name : $"{path}.{prop.Name}";
                        ValidateAgainstSchemaRecursive(prop.Value, propSchema, propPath, errors);
                    }
                }
            }
        }
        else if (schemaType == "string" && instance.ValueKind != JsonValueKind.String)
        {
            if (instance.ValueKind != JsonValueKind.Null) // Null might be acceptable
            {
                errors.Add(new ValidationError { Path = path, Message = $"Expected string but got {instance.ValueKind}" });
            }
        }
        else if (schemaType == "integer" && instance.ValueKind != JsonValueKind.Number)
        {
            if (instance.ValueKind != JsonValueKind.Null)
            {
                errors.Add(new ValidationError { Path = path, Message = $"Expected integer but got {instance.ValueKind}" });
            }
        }
        else if (schemaType == "boolean" && instance.ValueKind != JsonValueKind.True && instance.ValueKind != JsonValueKind.False)
        {
            if (instance.ValueKind != JsonValueKind.Null)
            {
                errors.Add(new ValidationError { Path = path, Message = $"Expected boolean but got {instance.ValueKind}" });
            }
        }
    }

    private JsonElement ReconstructJsonFromForm(Dictionary<string, string> formData)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartObject();
        foreach (var kvp in formData.Where(x => !x.Key.StartsWith("__") && x.Key != "path" && x.Key != "editMode"))
        {
            // Try to parse numbers
            if (int.TryParse(kvp.Value, out var intVal))
            {
                writer.WriteNumber(kvp.Key, intVal);
            }
            else if (bool.TryParse(kvp.Value, out var boolVal))
            {
                writer.WriteBoolean(kvp.Key, boolVal);
            }
            else
            {
                writer.WriteString(kvp.Key, kvp.Value);
            }
        }
        writer.WriteEndObject();
        writer.Flush();

        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static string GetDisplayName(string key)
    {
        // Convert camelCase or snake_case to Title Case
        return System.Text.RegularExpressions.Regex.Replace(
            key.Replace("_", " "),
            "([a-z])([A-Z])",
            "$1 $2"
        ).Trim();
    }

    private static string GetJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "",
            _ => element.GetRawText()
        };
    }

    private static string GetFieldType(JsonElement value, JsonElement? schema)
    {
        if (schema.HasValue && schema.Value.TryGetProperty("type", out var typeProp))
        {
            var typeStr = typeProp.GetString();
            if (typeStr == "string" && schema.Value.TryGetProperty("description", out var desc))
            {
                var descStr = desc.GetString() ?? "";
                if (descStr.Contains("longer") || descStr.Contains("paragraph") || descStr.Contains("description"))
                    return "textarea";
            }
            return typeStr ?? "text";
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => "number",
            JsonValueKind.True or JsonValueKind.False => "checkbox",
            JsonValueKind.String when value.GetString()?.Length > 100 => "textarea",
            _ => "text"
        };
    }

    private static bool IsRequiredField(string key, JsonElement? schema)
    {
        if (!schema.HasValue) return false;
        
        if (schema.Value.TryGetProperty("required", out var required))
        {
            return required.EnumerateArray().Any(r => r.GetString() == key);
        }

        return false;
    }

    private static string? GetFieldDescription(string key, JsonElement? schema)
    {
        if (!schema.HasValue) return null;

        if (schema.Value.TryGetProperty("properties", out var props) &&
            props.TryGetProperty(key, out var prop) &&
            prop.TryGetProperty("description", out var desc))
        {
            return desc.GetString();
        }

        return null;
    }
}

public class FormField
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public bool IsRequired { get; set; }
    public string? Description { get; set; }
}

public class ValidationError
{
    public string Path { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ValidationRequest
{
    [Required]
    public string Path { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}

public class GitCommitInfo
{
    public string Hash { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;

    public string ShortHash => Hash.Length > 7 ? Hash[..7] : Hash;
}

public enum DiffLineType
{
    Unchanged,
    Added,
    Removed
}

public class DiffLine
{
    public DiffLineType Type { get; set; }
    public int? OldLineNumber { get; set; }
    public int? NewLineNumber { get; set; }
    public string Content { get; set; } = string.Empty;
}
