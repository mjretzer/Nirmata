using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Workspace;

public class IndexModel : PageModel
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;
    private const string RecentWorkspacesKey = "RecentWorkspaces";
    private const string SelectedWorkspaceConfigKey = "SelectedWorkspacePath";

    public IndexModel(IWebHostEnvironment environment, ILogger<IndexModel> logger, IConfiguration configuration)
    {
        _environment = environment;
        _logger = logger;
        _configuration = configuration;
    }

    [BindProperty]
    public string WorkspacePath { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    public WorkspaceHealthReport? HealthReport { get; set; }
    public List<RecentWorkspace> RecentWorkspaces { get; set; } = new();
    public string? SelectedWorkspacePath { get; set; }

    public void OnGet()
    {
        LoadRecentWorkspaces();
        LoadSelectedWorkspace();
        
        // Populate the input with the currently selected workspace
        if (!string.IsNullOrEmpty(SelectedWorkspacePath))
        {
            WorkspacePath = SelectedWorkspacePath;
        }
    }

    public IActionResult OnPostClearWorkspace()
    {
        try
        {
            var configPath = GetWorkspaceConfigPath();
            if (System.IO.File.Exists(configPath))
            {
                System.IO.File.Delete(configPath);
                _logger.LogInformation("Cleared workspace selection, deleted: {Path}", configPath);
            }
            SelectedWorkspacePath = null;
            WorkspacePath = string.Empty;
            SuccessMessage = "Workspace selection cleared.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear workspace selection");
            ErrorMessage = "Failed to clear workspace selection.";
        }
        
        LoadRecentWorkspaces();
        return Page();
    }

    public IActionResult OnPostSelect(string workspacePath)
    {
        WorkspacePath = workspacePath;
        LoadRecentWorkspaces();

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            ErrorMessage = "Please provide a workspace path.";
            return Page();
        }

        if (!IsPathSafe(workspacePath))
        {
            ErrorMessage = "Invalid workspace path. Path traversal detected or path is outside allowed roots.";
            _logger.LogWarning("Path validation failed for: {Path}", workspacePath);
            return Page();
        }

        var fullPath = Path.GetFullPath(workspacePath);

        if (!Directory.Exists(fullPath))
        {
            ErrorMessage = $"Directory does not exist: {fullPath}";
            return Page();
        }

        var aosPath = Path.Combine(fullPath, ".aos");

        if (System.IO.File.Exists(aosPath))
        {
            ErrorMessage = ".aos exists as a file instead of a directory. Please remove the file and try again.";
            return Page();
        }

        HealthReport = PerformHealthCheck(fullPath);

        if (!HealthReport.IsHealthy)
        {
            ErrorMessage = "Workspace health check failed. See details below.";
        }
        else
        {
            SuccessMessage = "Workspace is healthy and ready.";
            AddToRecentWorkspaces(fullPath);
            SaveSelectedWorkspace(fullPath);
            SelectedWorkspacePath = fullPath;
        }

        return Page();
    }

    public IActionResult OnPostInitialize(string workspacePath)
    {
        WorkspacePath = workspacePath;
        LoadRecentWorkspaces();

        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            ErrorMessage = "Please provide a workspace path.";
            return Page();
        }

        if (!IsPathSafe(workspacePath))
        {
            ErrorMessage = "Invalid workspace path. Path traversal detected or path is outside allowed roots.";
            _logger.LogWarning("Path validation failed for: {Path}", workspacePath);
            return Page();
        }

        var fullPath = Path.GetFullPath(workspacePath);

        if (!Directory.Exists(fullPath))
        {
            try
            {
                Directory.CreateDirectory(fullPath);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Failed to create directory: {ex.Message}";
                _logger.LogError(ex, "Failed to create workspace directory: {Path}", fullPath);
                return Page();
            }
        }

        try
        {
            InitializeAosDirectory(fullPath);
            SuccessMessage = "AOS workspace initialized successfully.";
            AddToRecentWorkspaces(fullPath);
            SaveSelectedWorkspace(fullPath);
            SelectedWorkspacePath = fullPath;
            HealthReport = PerformHealthCheck(fullPath);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to initialize workspace: {ex.Message}";
            _logger.LogError(ex, "Failed to initialize AOS workspace: {Path}", fullPath);
        }

        return Page();
    }

    private static bool IsPathSafe(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Reject paths with null bytes (common attack vector)
        if (path.Contains('\0'))
            return false;

        // Reject overly long paths
        if (path.Length > 260)
            return false;

        // Get the fully qualified, canonical path
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception)
        {
            return false;
        }

        // Get the normalized canonical path for comparison
        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(Path.Combine(fullPath, "."));
        }
        catch (Exception)
        {
            return false;
        }

        // Ensure the normalized path equals the full path (no path traversal)
        if (!string.Equals(normalizedPath, fullPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Check for path traversal sequences in the original input
        // These patterns indicate attempts to escape the intended directory
        var pathTraversalPattern = new Regex(@"(\.\.[/\\]|\.\.$|^\.\.|~/|\\\.\\|/\.\./)");
        if (pathTraversalPattern.IsMatch(path))
        {
            // Additional check: ensure the resolved path is not outside allowed roots
            // If path contains .. we need to verify it doesn't escape
            if (path.Contains(".."))
            {
                var baseDir = Path.GetDirectoryName(fullPath);
                while (!string.IsNullOrEmpty(baseDir))
                {
                    if (baseDir.EndsWith(".."))
                    {
                        return false;
                    }
                    baseDir = Path.GetDirectoryName(baseDir);
                }
            }
        }

        // On Windows, ensure the path is not trying to escape to system directories
        if (OperatingSystem.IsWindows())
        {
            var windowsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var systemRoot = Environment.GetEnvironmentVariable("SystemRoot") ?? "";

            // Normalize for comparison
            fullPath = fullPath.TrimEnd('\\') + "\\";
            
            if (!string.IsNullOrEmpty(windowsFolder) && 
                fullPath.StartsWith(windowsFolder.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(programFiles) && 
                fullPath.StartsWith(programFiles.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(programData) && 
                fullPath.StartsWith(programData.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(systemRoot) && 
                fullPath.StartsWith(systemRoot.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Ensure no control characters
        if (path.Any(c => char.IsControl(c)))
        {
            return false;
        }

        return true;
    }

    internal static WorkspaceHealthReport PerformHealthCheck(string repositoryRootPath)
    {
        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");

        if (System.IO.File.Exists(aosRootPath))
        {
            return WorkspaceHealthReport.Failed(
                new[] { ".aos/ (expected directory, found file)" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                LockStatus.Unknown,
                null);
        }

        if (!Directory.Exists(aosRootPath))
        {
            return WorkspaceHealthReport.Failed(
                new[] { ".aos/ (directory not found)" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>(),
                LockStatus.Unknown,
                null);
        }

        var missingDirectories = new List<string>();
        var invalidDirectories = new List<string>();
        var missingFiles = new List<string>();
        var invalidFiles = new List<string>();
        var extraEntries = new List<string>();
        var schemaValidationErrors = new List<string>();

        var expectedTopLevel = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks"
        };

        foreach (var dirName in expectedTopLevel)
        {
            var fullPath = Path.Combine(aosRootPath, dirName);
            if (!Directory.Exists(fullPath))
            {
                if (System.IO.File.Exists(fullPath))
                {
                    invalidDirectories.Add($".aos/{dirName} (expected directory, found file)");
                }
                else
                {
                    missingDirectories.Add($".aos/{dirName}");
                }
            }
        }

        foreach (var entry in Directory.EnumerateFileSystemEntries(aosRootPath))
        {
            var name = Path.GetFileName(entry);
            if (!string.IsNullOrWhiteSpace(name) && !expectedTopLevel.Contains(name))
            {
                extraEntries.Add($".aos/{name}");
            }
        }

        // Validate required files with JSON validation
        ValidateRequiredJsonFile(aosRootPath, ".aos/spec/project.json", "spec/project.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/spec/roadmap.json", "spec/roadmap.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/spec/milestones/index.json", "spec/milestones/index.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/spec/phases/index.json", "spec/phases/index.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/spec/tasks/index.json", "spec/tasks/index.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/spec/issues/index.json", "spec/issues/index.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/spec/uat/index.json", "spec/uat/index.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/state/state.json", "state/state.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredFile(aosRootPath, ".aos/state/events.ndjson", "state/events.ndjson", missingFiles, invalidFiles);
        ValidateRequiredJsonFile(aosRootPath, ".aos/evidence/logs/commands.json", "evidence/logs/commands.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/evidence/runs/index.json", "evidence/runs/index.json", missingFiles, invalidFiles, schemaValidationErrors);
        ValidateRequiredJsonFile(aosRootPath, ".aos/schemas/registry.json", "schemas/registry.json", missingFiles, invalidFiles, schemaValidationErrors);

        // Check workspace lock status
        var (lockStatus, lockInfo) = CheckLockStatus(aosRootPath);

        return WorkspaceHealthReport.FromChecks(missingDirectories, invalidDirectories, missingFiles, invalidFiles, extraEntries, schemaValidationErrors, lockStatus, lockInfo);
    }

    private static (LockStatus Status, LockInfo? Info) CheckLockStatus(string aosRootPath)
    {
        var lockPath = Path.Combine(aosRootPath, "locks", "workspace.lock");

        if (!System.IO.File.Exists(lockPath))
        {
            return (LockStatus.Unlocked, null);
        }

        try
        {
            var json = System.IO.File.ReadAllText(lockPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var holder = root.TryGetProperty("holder", out var holderProp) ? holderProp : default;
            var command = holder.TryGetProperty("command", out var cmdProp) ? cmdProp.GetString() : null;
            var pid = holder.TryGetProperty("pid", out var pidProp) ? pidProp.GetInt32() : (int?)null;
            var machine = holder.TryGetProperty("machine", out var machineProp) ? machineProp.GetString() : null;
            var user = holder.TryGetProperty("user", out var userProp) ? userProp.GetString() : null;
            var acquiredAt = root.TryGetProperty("acquiredAtUtc", out var timeProp) ? timeProp.GetString() : null;

            // Check if the process is still running
            bool isActive = false;
            if (pid.HasValue && OperatingSystem.IsWindows())
            {
                try
                {
                    var process = System.Diagnostics.Process.GetProcessById(pid.Value);
                    isActive = process != null && !process.HasExited;
                }
                catch (ArgumentException)
                {
                    // Process not found
                    isActive = false;
                }
            }

            var lockInfo = new LockInfo
            {
                Command = command ?? "unknown",
                Pid = pid,
                Machine = machine ?? "unknown",
                User = user ?? "unknown",
                AcquiredAtUtc = acquiredAt,
                IsActive = isActive
            };

            return (isActive ? LockStatus.LockedActive : LockStatus.LockedStale, lockInfo);
        }
        catch (Exception)
        {
            return (LockStatus.LockedUnknown, new LockInfo { Command = "unknown", IsActive = false });
        }
    }

    private static void ValidateRequiredJsonFile(string aosRootPath, string contractPath, string relativePath, List<string> missingFiles, List<string> invalidFiles, List<string> schemaValidationErrors)
    {
        var filePath = Path.Combine(aosRootPath, relativePath);

        if (System.IO.File.Exists(filePath))
        {
            if (Directory.Exists(filePath))
            {
                invalidFiles.Add($"{contractPath} (expected file, found directory)");
                return;
            }

            try
            {
                using var stream = System.IO.File.OpenRead(filePath);
                using var doc = JsonDocument.Parse(stream);

                // Basic schema validation: check for required schema version field
                if (doc.RootElement.TryGetProperty("schemaVersion", out var schemaVersionProp))
                {
                    if (schemaVersionProp.ValueKind != JsonValueKind.Number)
                    {
                        schemaValidationErrors.Add($"{contractPath} (schemaVersion must be a number)");
                    }
                }
                else
                {
                    // Also accept SchemaVersion (PascalCase)
                    if (!doc.RootElement.TryGetProperty("SchemaVersion", out var _))
                    {
                        schemaValidationErrors.Add($"{contractPath} (missing schemaVersion field)");
                    }
                }

                // Additional validation: ensure root is an object
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    schemaValidationErrors.Add($"{contractPath} (root must be an object)");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                invalidFiles.Add($"{contractPath} (invalid JSON)");
            }
            return;
        }

        if (Directory.Exists(filePath))
        {
            invalidFiles.Add($"{contractPath} (expected file, found directory)");
            return;
        }

        missingFiles.Add(contractPath);
    }

    private static void ValidateRequiredFile(string aosRootPath, string contractPath, string relativePath, List<string> missingFiles, List<string> invalidFiles)
    {
        var filePath = Path.Combine(aosRootPath, relativePath);

        if (System.IO.File.Exists(filePath))
        {
            if (Directory.Exists(filePath))
            {
                invalidFiles.Add($"{contractPath} (expected file, found directory)");
            }
            return;
        }

        if (Directory.Exists(filePath))
        {
            invalidFiles.Add($"{contractPath} (expected file, found directory)");
            return;
        }

        missingFiles.Add(contractPath);
    }

    private void InitializeAosDirectory(string repositoryRootPath)
    {
        var aosRootPath = Path.Combine(repositoryRootPath, ".aos");

        // Create canonical top-level directories
        var canonicalDirectories = new[]
        {
            "spec", "state", "evidence", "context", "codebase", "cache", "config", "schemas", "locks"
        };

        foreach (var dir in canonicalDirectories)
        {
            Directory.CreateDirectory(Path.Combine(aosRootPath, dir));
        }

        // Create additional subdirectories
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "milestones"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "phases"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "tasks"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "issues"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "spec", "uat"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "evidence", "logs"));
        Directory.CreateDirectory(Path.Combine(aosRootPath, "evidence", "runs"));

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        // Create baseline spec files
        WriteJsonIfMissing(Path.Combine(aosRootPath, "spec", "project.json"),
            new { SchemaVersion = 1, Project = new { Name = "", Description = "" } }, jsonOptions);

        WriteJsonIfMissing(Path.Combine(aosRootPath, "spec", "roadmap.json"),
            new { SchemaVersion = 1, Roadmap = new { Title = "", Items = Array.Empty<object>() } }, jsonOptions);

        WriteJsonIfMissing(Path.Combine(aosRootPath, "spec", "milestones", "index.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() }, jsonOptions);

        WriteJsonIfMissing(Path.Combine(aosRootPath, "spec", "phases", "index.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() }, jsonOptions);

        WriteJsonIfMissing(Path.Combine(aosRootPath, "spec", "tasks", "index.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() }, jsonOptions);

        WriteJsonIfMissing(Path.Combine(aosRootPath, "spec", "issues", "index.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() }, jsonOptions);

        WriteJsonIfMissing(Path.Combine(aosRootPath, "spec", "uat", "index.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() }, jsonOptions);

        // Create baseline state files
        WriteJsonIfMissing(Path.Combine(aosRootPath, "state", "state.json"),
            new
            {
                SchemaVersion = 1,
                Cursor = new { MilestoneId = (string?)null, PhaseId = (string?)null, TaskId = (string?)null, StepId = (string?)null },
                Blockers = Array.Empty<object>(),
                Status = "idle"
            }, jsonOptions);

        // Create empty events file if missing
        var eventsPath = Path.Combine(aosRootPath, "state", "events.ndjson");
        if (!System.IO.File.Exists(eventsPath))
        {
            System.IO.File.WriteAllText(eventsPath, "");
        }

        // Create baseline evidence files
        WriteJsonIfMissing(Path.Combine(aosRootPath, "evidence", "logs", "commands.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() }, jsonOptions);

        WriteJsonIfMissing(Path.Combine(aosRootPath, "evidence", "runs", "index.json"),
            new { SchemaVersion = 1, Items = Array.Empty<object>() }, jsonOptions);

        // Create schemas registry
        WriteJsonIfMissing(Path.Combine(aosRootPath, "schemas", "registry.json"),
            new { SchemaVersion = 1, Schemas = Array.Empty<string>() }, jsonOptions);

        // Create config policy
        WriteJsonIfMissing(Path.Combine(aosRootPath, "config", "policy.json"),
            new
            {
                SchemaVersion = 1,
                ScopeAllowlist = new { Write = new[] { ".aos/" } },
                ToolAllowlist = new { Tools = Array.Empty<string>(), Providers = Array.Empty<string>() },
                NoImplicitState = true
            }, jsonOptions);
    }

    private static void WriteJsonIfMissing(string filePath, object content, JsonSerializerOptions options)
    {
        if (!System.IO.File.Exists(filePath))
        {
            var json = JsonSerializer.Serialize(content, options);
            System.IO.File.WriteAllText(filePath, json);
        }
    }

    private void LoadRecentWorkspaces()
    {
        var data = HttpContext.Session.GetString(RecentWorkspacesKey);
        if (!string.IsNullOrEmpty(data))
        {
            try
            {
                RecentWorkspaces = JsonSerializer.Deserialize<List<RecentWorkspace>>(data) ?? new List<RecentWorkspace>();
            }
            catch
            {
                RecentWorkspaces = new List<RecentWorkspace>();
            }
        }
    }

    private void AddToRecentWorkspaces(string path)
    {
        // Remove if already exists (to move to top)
        RecentWorkspaces.RemoveAll(w => w.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        // Add to top
        RecentWorkspaces.Insert(0, new RecentWorkspace
        {
            Path = path,
            LastAccessed = DateTime.UtcNow
        });

        // Keep only last 10
        if (RecentWorkspaces.Count > 10)
        {
            RecentWorkspaces = RecentWorkspaces.Take(10).ToList();
        }

        // Save to session
        var data = JsonSerializer.Serialize(RecentWorkspaces);
        HttpContext.Session.SetString(RecentWorkspacesKey, data);
    }

    private void LoadSelectedWorkspace()
    {
        try
        {
            var configPath = GetWorkspaceConfigPath();
            _logger.LogInformation("Loading workspace config from: {Path}", configPath);
            
            if (System.IO.File.Exists(configPath))
            {
                var json = System.IO.File.ReadAllText(configPath);
                _logger.LogInformation("Workspace config content: {Json}", json);
                
                var config = JsonSerializer.Deserialize<WorkspaceConfig>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                if (config?.SelectedWorkspacePath != null)
                {
                    if (Directory.Exists(config.SelectedWorkspacePath))
                    {
                        SelectedWorkspacePath = config.SelectedWorkspacePath;
                        _logger.LogInformation("Loaded selected workspace: {Path}", SelectedWorkspacePath);
                    }
                    else
                    {
                        _logger.LogWarning("Saved workspace path no longer exists: {Path}", config.SelectedWorkspacePath);
                    }
                }
            }
            else
            {
                _logger.LogInformation("No workspace config file found at: {Path}", configPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load selected workspace configuration from: {Path}", GetWorkspaceConfigPath());
        }
    }

    private void SaveSelectedWorkspace(string path)
    {
        try
        {
            var configPath = GetWorkspaceConfigPath();
            var configDir = Path.GetDirectoryName(configPath);
            
            _logger.LogInformation("Saving workspace to: {Path}, directory: {Dir}", configPath, configDir);
            
            if (!string.IsNullOrEmpty(configDir) && !Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
                _logger.LogInformation("Created directory: {Dir}", configDir);
            }

            var config = new WorkspaceConfig
            {
                SelectedWorkspacePath = path,
                LastUpdated = DateTime.UtcNow
            };

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(config, jsonOptions);
            System.IO.File.WriteAllText(configPath, json);
            _logger.LogInformation("Saved workspace config: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save selected workspace to: {Path}", path);
        }
    }

    private string GetWorkspaceConfigPath()
    {
        // Store in user's local application data
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }
}

public class WorkspaceConfig
{
    public string? SelectedWorkspacePath { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class WorkspaceHealthReport
{
    public bool IsHealthy { get; set; }
    public List<string> MissingDirectories { get; set; } = new();
    public List<string> InvalidDirectories { get; set; } = new();
    public List<string> MissingFiles { get; set; } = new();
    public List<string> InvalidFiles { get; set; } = new();
    public List<string> ExtraEntries { get; set; } = new();
    public List<string> SchemaValidationErrors { get; set; } = new();
    public LockStatus LockStatus { get; set; }
    public LockInfo? LockInfo { get; set; }

    public static WorkspaceHealthReport FromChecks(
        List<string> missingDirectories,
        List<string> invalidDirectories,
        List<string> missingFiles,
        List<string> invalidFiles,
        List<string> extraEntries,
        List<string> schemaValidationErrors,
        LockStatus lockStatus,
        LockInfo? lockInfo)
    {
        return new WorkspaceHealthReport
        {
            IsHealthy = missingDirectories.Count == 0 &&
                       invalidDirectories.Count == 0 &&
                       missingFiles.Count == 0 &&
                       invalidFiles.Count == 0 &&
                       schemaValidationErrors.Count == 0,
            MissingDirectories = missingDirectories,
            InvalidDirectories = invalidDirectories,
            MissingFiles = missingFiles,
            InvalidFiles = invalidFiles,
            ExtraEntries = extraEntries,
            SchemaValidationErrors = schemaValidationErrors,
            LockStatus = lockStatus,
            LockInfo = lockInfo
        };
    }

    public static WorkspaceHealthReport Failed(
        string[] missingDirectories,
        string[] invalidDirectories,
        string[] missingFiles,
        string[] invalidFiles,
        string[] extraEntries,
        string[] schemaValidationErrors,
        LockStatus lockStatus,
        LockInfo? lockInfo)
    {
        return new WorkspaceHealthReport
        {
            IsHealthy = false,
            MissingDirectories = missingDirectories.ToList(),
            InvalidDirectories = invalidDirectories.ToList(),
            MissingFiles = missingFiles.ToList(),
            InvalidFiles = invalidFiles.ToList(),
            ExtraEntries = extraEntries.ToList(),
            SchemaValidationErrors = schemaValidationErrors.ToList(),
            LockStatus = lockStatus,
            LockInfo = lockInfo
        };
    }
}

public enum LockStatus
{
    Unknown,
    Unlocked,
    LockedActive,
    LockedStale,
    LockedUnknown
}

public class LockInfo
{
    public string Command { get; set; } = string.Empty;
    public int? Pid { get; set; }
    public string Machine { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string? AcquiredAtUtc { get; set; }
    public bool IsActive { get; set; }
}

public class RecentWorkspace
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastAccessed { get; set; }
}
