using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Specs;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;

    public IndexModel(ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public string? WorkspacePath { get; set; }
    public string? WorkspaceName => !string.IsNullOrEmpty(WorkspacePath)
        ? Path.GetFileName(WorkspacePath)
        : null;
    public string? ErrorMessage { get; set; }
    public string? SearchQuery { get; set; }
    public SpecTreeNode? SpecTree { get; set; }
    public SpecFile? SelectedFile { get; set; }
    public List<SpecSearchResult> SearchResults { get; set; } = new();
    public bool IsEditMode { get; set; }
    public int TotalFiles { get; set; }
    public List<SpecCategory> SpecCategories { get; set; } = new();

    public void OnGet(string? search = null, string? file = null, bool edit = false)
    {
        SearchQuery = search;
        IsEditMode = edit;

        LoadWorkspace();

        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var specPath = Path.Combine(WorkspacePath, ".aos", "spec");
        if (!Directory.Exists(specPath))
        {
            ErrorMessage = "Spec directory does not exist. Please initialize the workspace first.";
            return;
        }

        // Build tree
        SpecTree = BuildSpecTree(specPath);

        // Count totals
        TotalFiles = CountFiles(SpecTree);
        SpecCategories = GetSpecCategories(SpecTree);

        // Handle search
        if (!string.IsNullOrEmpty(search))
        {
            SearchResults = SearchSpecs(specPath, search);
        }

        // Handle file selection
        if (!string.IsNullOrEmpty(file))
        {
            SelectedFile = LoadSpecFile(specPath, file);
        }
    }

    private SpecTreeNode BuildSpecTree(string specPath)
    {
        var root = new SpecTreeNode
        {
            Name = "spec",
            RelativePath = "",
            IsDirectory = true,
            Icon = "📁"
        };

        var categories = new[] { "project", "roadmap", "milestones", "phases", "tasks", "issues", "uat" };

        foreach (var category in categories)
        {
            var categoryPath = Path.Combine(specPath, category);
            if (Directory.Exists(categoryPath))
            {
                var categoryNode = new SpecTreeNode
                {
                    Name = category,
                    RelativePath = category,
                    IsDirectory = true,
                    Icon = "📁"
                };

                AddDirectoryContents(categoryNode, categoryPath, specPath);
                root.Children.Add(categoryNode);
            }
        }

        // Add other files/folders not in standard categories
        foreach (var dir in Directory.GetDirectories(specPath))
        {
            var dirName = Path.GetFileName(dir);
            if (!categories.Contains(dirName, StringComparer.OrdinalIgnoreCase))
            {
                var node = new SpecTreeNode
                {
                    Name = dirName,
                    RelativePath = dirName,
                    IsDirectory = true,
                    Icon = "📁"
                };
                AddDirectoryContents(node, dir, specPath);
                root.Children.Add(node);
            }
        }

        foreach (var file in Directory.GetFiles(specPath))
        {
            var fileName = Path.GetFileName(file);
            root.Children.Add(new SpecTreeNode
            {
                Name = fileName,
                RelativePath = fileName,
                IsDirectory = false,
                Icon = GetFileIcon(fileName)
            });
        }

        return root;
    }

    private void AddDirectoryContents(SpecTreeNode parent, string dirPath, string specPath)
    {
        // Add subdirectories
        foreach (var dir in Directory.GetDirectories(dirPath))
        {
            var dirName = Path.GetFileName(dir);
            var relativePath = Path.GetRelativePath(specPath, dir).Replace("\\", "/");
            var node = new SpecTreeNode
            {
                Name = dirName,
                RelativePath = relativePath,
                IsDirectory = true,
                Icon = "📁"
            };
            AddDirectoryContents(node, dir, specPath);
            parent.Children.Add(node);
        }

        // Add files
        foreach (var file in Directory.GetFiles(dirPath))
        {
            var fileName = Path.GetFileName(file);
            var relativePath = Path.GetRelativePath(specPath, file).Replace("\\", "/");
            parent.Children.Add(new SpecTreeNode
            {
                Name = fileName,
                RelativePath = relativePath,
                IsDirectory = false,
                Icon = GetFileIcon(fileName)
            });
        }
    }

    private string GetFileIcon(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
        {
            ".json" => "📄",
            ".md" => "📝",
            ".txt" => "📃",
            ".yml" or ".yaml" => "⚙️",
            _ => "📄"
        };
    }

    private int CountFiles(SpecTreeNode? node)
    {
        if (node == null) return 0;
        if (!node.IsDirectory) return 1;
        return node.Children.Sum(CountFiles);
    }

    private List<SpecCategory> GetSpecCategories(SpecTreeNode? root)
    {
        if (root == null) return new List<SpecCategory>();

        return root.Children
            .Where(c => c.IsDirectory)
            .Select(c => new SpecCategory
            {
                Name = c.Name,
                FileCount = CountFiles(c)
            })
            .ToList();
    }

    private List<SpecSearchResult> SearchSpecs(string specPath, string query)
    {
        var results = new List<SpecSearchResult>();
        var lowerQuery = query.ToLowerInvariant();

        try
        {
            // Search JSON files
            foreach (var file in Directory.GetFiles(specPath, "*.json", SearchOption.AllDirectories))
            {
                SearchFile(file, specPath, lowerQuery, results, "json");
            }

            // Search Markdown files
            foreach (var file in Directory.GetFiles(specPath, "*.md", SearchOption.AllDirectories))
            {
                SearchFile(file, specPath, lowerQuery, results, "markdown");
            }

            // Search YAML files
            foreach (var file in Directory.GetFiles(specPath, "*.yml", SearchOption.AllDirectories))
            {
                SearchFile(file, specPath, lowerQuery, results, "yaml");
            }
            foreach (var file in Directory.GetFiles(specPath, "*.yaml", SearchOption.AllDirectories))
            {
                SearchFile(file, specPath, lowerQuery, results, "yaml");
            }

            // Search text files
            foreach (var file in Directory.GetFiles(specPath, "*.txt", SearchOption.AllDirectories))
            {
                SearchFile(file, specPath, lowerQuery, results, "text");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching specs");
        }

        return results.OrderByDescending(r => r.LastModified).ToList();
    }

    private void SearchFile(string filePath, string specPath, string lowerQuery, List<SpecSearchResult> results, string fileType)
    {
        var fileName = Path.GetFileName(filePath);
        var relativePath = Path.GetRelativePath(specPath, filePath).Replace("\\", "/");
        var matches = false;
        string? matchingContent = null;

        // Check filename
        if (fileName.ToLowerInvariant().Contains(lowerQuery))
        {
            matches = true;
        }

        // Check content
        if (!matches)
        {
            try
            {
                var content = System.IO.File.ReadAllText(filePath);
                if (content.ToLowerInvariant().Contains(lowerQuery))
                {
                    matches = true;
                    // Extract snippet around match
                    var index = content.ToLowerInvariant().IndexOf(lowerQuery);
                    var start = Math.Max(0, index - 60);
                    var length = Math.Min(120, content.Length - start);
                    matchingContent = "..." + content.Substring(start, length).Trim() + "...";
                }
            }
            catch { }
        }

        if (matches)
        {
            results.Add(new SpecSearchResult
            {
                RelativePath = relativePath,
                FileType = fileType,
                LastModified = System.IO.File.GetLastWriteTime(filePath),
                MatchingContent = matchingContent
            });
        }
    }

    private SpecFile? LoadSpecFile(string specPath, string relativePath)
    {
        var fullPath = Path.Combine(specPath, relativePath.Replace("/", "\\"));

        if (!System.IO.File.Exists(fullPath))
        {
            return null;
        }

        try
        {
            var content = System.IO.File.ReadAllText(fullPath);
            var isJson = Path.GetExtension(fullPath).Equals(".json", StringComparison.OrdinalIgnoreCase);

            return new SpecFile
            {
                Name = Path.GetFileName(fullPath),
                RelativePath = relativePath,
                Content = content,
                IsJson = isJson,
                HighlightedContent = isJson ? HighlightJson(content) : null,
                LastModified = System.IO.File.GetLastWriteTime(fullPath)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading spec file: {Path}", relativePath);
            return null;
        }
    }

    public static string HighlightJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            json = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
        }
        catch { }

        var highlighted = new System.Text.StringBuilder();
        var inString = false;
        var escapeNext = false;

        for (int i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (escapeNext)
            {
                highlighted.Append(c);
                escapeNext = false;
                continue;
            }

            if (c == '\\')
            {
                escapeNext = true;
                highlighted.Append(c);
                continue;
            }

            if (c == '"' && !inString)
            {
                inString = true;
                highlighted.Append("<span class=\"json-key\">\"");
                continue;
            }

            if (c == '"' && inString)
            {
                inString = false;
                highlighted.Append("\"</span>");
                continue;
            }

            if (inString)
            {
                highlighted.Append(c);
                continue;
            }

            if (c == '{' || c == '}' || c == '[' || c == ']')
            {
                highlighted.Append($"<span class=\"json-bracket\">{c}</span>");
            }
            else if (c == ':')
            {
                highlighted.Append("<span class=\"json-colon\">:</span>");
            }
            else if (c == ',')
            {
                highlighted.Append("<span class=\"json-comma\">,</span>");
            }
            else if (char.IsDigit(c) || (c == '-' && i + 1 < json.Length && char.IsDigit(json[i + 1])))
            {
                var numStart = i;
                while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == 'e' || json[i] == 'E' || json[i] == '-' || json[i] == '+'))
                {
                    i++;
                }
                var number = json.Substring(numStart, i - numStart);
                highlighted.Append($"<span class=\"json-number\">{number}</span>");
                i--;
            }
            else if (json.Substring(i).StartsWith("true", StringComparison.OrdinalIgnoreCase) &&
                     (i + 4 >= json.Length || !char.IsLetterOrDigit(json[i + 4])))
            {
                highlighted.Append("<span class=\"json-boolean\">true</span>");
                i += 3;
            }
            else if (json.Substring(i).StartsWith("false", StringComparison.OrdinalIgnoreCase) &&
                     (i + 5 >= json.Length || !char.IsLetterOrDigit(json[i + 5])))
            {
                highlighted.Append("<span class=\"json-boolean\">false</span>");
                i += 4;
            }
            else if (json.Substring(i).StartsWith("null", StringComparison.OrdinalIgnoreCase) &&
                     (i + 4 >= json.Length || !char.IsLetterOrDigit(json[i + 4])))
            {
                highlighted.Append("<span class=\"json-null\">null</span>");
                i += 3;
            }
            else
            {
                highlighted.Append(c);
            }
        }

        return highlighted.ToString();
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

            if (string.IsNullOrEmpty(WorkspacePath))
            {
                ErrorMessage = "No workspace selected. Please select a workspace first.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load workspace configuration");
            ErrorMessage = "Failed to load workspace configuration.";
        }
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }
}

public class SpecTreeNode
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool IsDirectory { get; set; }
    public string Icon { get; set; } = "📄";
    public List<SpecTreeNode> Children { get; set; } = new();
}

public class SpecFile
{
    public string Name { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsJson { get; set; }
    public string? HighlightedContent { get; set; }
    public DateTime LastModified { get; set; }

    public string GetDiskPath(string workspacePath)
    {
        return Path.Combine(workspacePath, ".aos", "spec", RelativePath.Replace("/", "\\"));
    }
}

public class SpecSearchResult
{
    public string RelativePath { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string? MatchingContent { get; set; }
}

public class SpecCategory
{
    public string Name { get; set; } = string.Empty;
    public int FileCount { get; set; }
}

public class WorkspaceConfig
{
    public string? SelectedWorkspacePath { get; set; }
}
