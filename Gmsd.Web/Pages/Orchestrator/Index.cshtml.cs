using System.Text.Json;
using System.Text;
using Gmsd.Agents.Execution.ControlPlane;
using Gmsd.Agents.Persistence.Runs;
using Gmsd.Aos.Engine.Validation;
using Gmsd.Aos.Engine.Workspace;
using Gmsd.Aos.Public;
using Gmsd.Web.AgentRunner;
using Gmsd.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Orchestrator;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly WorkflowClassifier? _agentRunner;
    private readonly IWorkspace? _workspace;
    private readonly IStateStore? _stateStore;
    private readonly IRunLifecycleManager? _runLifecycleManager;

    public IndexModel(
        ILogger<IndexModel> logger,
        IConfiguration configuration,
        WorkflowClassifier? agentRunner = null,
        IWorkspace? workspace = null,
        IStateStore? stateStore = null,
        IRunLifecycleManager? runLifecycleManager = null)
    {
        _logger = logger;
        _configuration = configuration;
        _agentRunner = agentRunner;
        _workspace = workspace;
        _stateStore = stateStore;
        _runLifecycleManager = runLifecycleManager;
    }

    public List<ChatMessage> Messages { get; set; } = new();
    public List<SlashCommand> AvailableCommands { get; set; } = new();
    public string? WorkspacePath { get; set; }
    public string? WorkspaceName => !string.IsNullOrEmpty(WorkspacePath)
        ? Path.GetFileName(WorkspacePath)
        : null;
    public string? ErrorMessage { get; set; }
    public string CurrentCommand { get; set; } = string.Empty;
    public SafetyRails SafetyRails { get; set; } = new();
    public List<EvidenceFile> EvidenceFiles { get; set; } = new();

    public void OnGet()
    {
        InitializeCommands();
        LoadWorkspace();
        LoadMessageHistory();
        LoadSafetyRails();
        LoadEvidenceFiles();
    }

    public IActionResult OnPostSendCommand([FromForm] string command)
    {
        InitializeCommands();
        LoadWorkspace();
        LoadMessageHistory();
        LoadSafetyRails();
        LoadEvidenceFiles();

        if (string.IsNullOrWhiteSpace(command))
        {
            return Page();
        }

        CurrentCommand = command;

        // Reset touched files for new command
        SafetyRails.TouchedFiles.Clear();
        SafetyRails.LastCommand = command;

        var userMessage = new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Content = command,
            IsUser = true,
            Timestamp = DateTime.Now,
            Type = MessageType.UserInput
        };
        Messages.Add(userMessage);

        var response = ProcessCommand(command);
        Messages.Add(response);

        SaveMessageHistory();

        CurrentCommand = string.Empty;
        return Page();
    }

    private ChatMessage ProcessCommand(string command)
    {
        var parts = command.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var args = parts.Length > 1 ? string.Join(' ', parts[1..]) : string.Empty;

        _logger.LogInformation("Processing command: {Command}", command);

        return cmd switch
        {
            "/help" => ExecuteLocalCommandAsRun(command, success: true, localOutput: GetHelpText()),
            "/status" => ExecuteLocalCommandAsRun(command, success: true, localOutput: GetStatusText()),
            "/validate" => ExecuteLocalCommandAsRun(command, success: true, localOutput: GetValidateText()),
            "/init" => ExecuteLocalCommandAsRun(command, success: true, localOutput: GetInitText()),
            _ => ExecuteViaOrchestrator(command)
        };
    }

    private ChatMessage ExecuteViaOrchestrator(string command)
    {
        try
        {
            if (_agentRunner == null)
            {
                return CreateSystemMessage("Agent runner is not available.");
            }

            // Show toast notification
            this.ToastInfo($"Executing: {command}", "Agent Run Started");

            // Execute via the orchestrator (synchronously for page handler)
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var result = _agentRunner.ExecuteAsync(command, cts.Token).GetAwaiter().GetResult();

            var report = RunPreflight();
            var snapshot = TryReadSnapshotJson();
            var content = FormatStructuredTurn(
                rawCommand: command,
                normalizedIntent: NormalizeIntentForDisplay(command),
                preflight: report,
                routingDecision: GetRoutingDecision(result),
                runId: result.RunId,
                outputs: GetDefaultOutputsForRun(result.RunId),
                stateSnapshotJson: snapshot,
                nextAction: ChooseNextAction(report)
            );

            if (result.IsSuccess)
            {
                var runIdShort = result.RunId?[..Math.Min(8, result.RunId?.Length ?? 0)] ?? "unknown";
                this.ToastSuccess($"Run {runIdShort} completed successfully", "Agent Run Complete");

                return new ChatMessage
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Content = content,
                    IsUser = false,
                    Timestamp = DateTime.Now,
                    Type = MessageType.CommandResult,
                    HasStructuredData = true
                };
            }
            else
            {
                var runIdShort = result.RunId?[..Math.Min(8, result.RunId?.Length ?? 0)] ?? "unknown";
                this.ToastError($"Run {runIdShort} failed", "Agent Run Failed");

                return new ChatMessage
                {
                    Id = Guid.NewGuid().ToString("N")[..8],
                    Content = content,
                    IsUser = false,
                    Timestamp = DateTime.Now,
                    Type = MessageType.SystemMessage
                };
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Provider") || ex.Message.Contains("API key"))
        {
            _logger.LogError(ex, "LLM provider not configured");
            this.ToastError("LLM provider not configured", "Configuration Error");

            return new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Content = "⚠️ **LLM Not Configured**\n\nPlease configure your LLM provider in Settings > LLM Configuration before running agent commands.",
                IsUser = false,
                Timestamp = DateTime.Now,
                Type = MessageType.SystemMessage
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing command via orchestrator");
            this.ToastError($"Error: {ex.Message}", "Execution Error");

            return new ChatMessage
            {
                Id = Guid.NewGuid().ToString("N")[..8],
                Content = $"❌ **Error:** {ex.Message}",
                IsUser = false,
                Timestamp = DateTime.Now,
                Type = MessageType.SystemMessage
            };
        }
    }

    private ChatMessage GetHelpResponse()
    {
        var content = "Available commands:\n\n";
        foreach (var cmd in AvailableCommands)
        {
            content += $"**{cmd.Command}** - {cmd.Description}\n";
        }

        return new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Content = content,
            IsUser = false,
            Timestamp = DateTime.Now,
            Type = MessageType.CommandResult
        };
    }

    private ChatMessage GetStatusResponse()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return CreateSystemMessage("No workspace selected. Please select a workspace first using the Workspace page.");
        }

        var aosPath = Path.Combine(WorkspacePath, ".aos");
        if (!Directory.Exists(aosPath))
        {
            return CreateSystemMessage("Selected workspace does not have a valid .aos directory. Run `/init` to initialize.");
        }

        var state = LoadState(aosPath);
        var hasIssues = Directory.Exists(Path.Combine(aosPath, "spec", "issues"));
        var issueCount = hasIssues ? Directory.GetFiles(Path.Combine(aosPath, "spec", "issues"), "*.json").Length : 0;

        var content = $"**Workspace Status**\n\n";
        content += $"- **Path:** {WorkspacePath}\n";
        content += $"- **Status:** {(state != null ? state.Status : "unknown")}\n";
        content += $"- **Cursor:** {(state != null ? state.GetCursorDisplay() : "Not set")}\n";
        content += $"- **Open Issues:** {issueCount}\n";

        return new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Content = content,
            IsUser = false,
            Timestamp = DateTime.Now,
            Type = MessageType.CommandResult,
            HasStructuredData = true
        };
    }

    private ChatMessage GetValidateResponse()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return CreateSystemMessage("No workspace selected. Please select a workspace first.");
        }

        var aosPath = Path.Combine(WorkspacePath, ".aos");
        var validationItems = new List<ValidationItem>();

        // Check .aos directory
        var aosExists = Directory.Exists(aosPath);
        validationItems.Add(new ValidationItem
        {
            Name = ".aos directory exists",
            Status = aosExists ? ValidationStatus.Success : ValidationStatus.Error,
            Message = aosExists ? "Found" : "Not found"
        });

        if (aosExists)
        {
            // Check state.json
            var statePath = Path.Combine(aosPath, "state", "state.json");
            var stateExists = System.IO.File.Exists(statePath);
            validationItems.Add(new ValidationItem
            {
                Name = "state.json exists",
                Status = stateExists ? ValidationStatus.Success : ValidationStatus.Warning,
                Message = stateExists ? "Found" : "Not found"
            });

            // Check spec directory
            var specPath = Path.Combine(aosPath, "spec");
            var specExists = Directory.Exists(specPath);
            validationItems.Add(new ValidationItem
            {
                Name = "spec directory exists",
                Status = specExists ? ValidationStatus.Success : ValidationStatus.Warning,
                Message = specExists ? "Found" : "Not found"
            });

            // Check evidence directory
            var evidencePath = Path.Combine(aosPath, "evidence");
            var evidenceExists = Directory.Exists(evidencePath);
            validationItems.Add(new ValidationItem
            {
                Name = "evidence directory exists",
                Status = evidenceExists ? ValidationStatus.Success : ValidationStatus.Warning,
                Message = evidenceExists ? "Found" : "Not found"
            });
        }

        var overallStatus = validationItems.All(i => i.Status == ValidationStatus.Success)
            ? ValidationStatus.Success
            : validationItems.Any(i => i.Status == ValidationStatus.Error) ? ValidationStatus.Error : ValidationStatus.Warning;

        var html = RenderValidationReport(overallStatus, validationItems);

        return new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Content = $"Validation completed with {validationItems.Count(i => i.Status == ValidationStatus.Success)}/{validationItems.Count} checks passed.",
            IsUser = false,
            Timestamp = DateTime.Now,
            Type = MessageType.CommandResult,
            HasStructuredData = true,
            HtmlContent = html
        };
    }

    private ChatMessage GetInitResponse()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return CreateSystemMessage("No workspace selected. Please select a workspace first.");
        }

        var aosPath = Path.Combine(WorkspacePath, ".aos");

        if (Directory.Exists(aosPath))
        {
            return CreateSystemMessage("Workspace is already initialized (.aos directory exists).");
        }

        try
        {
            var touchedFiles = new List<string>();

            Directory.CreateDirectory(aosPath);
            touchedFiles.Add(".aos/");

            Directory.CreateDirectory(Path.Combine(aosPath, "state"));
            touchedFiles.Add(".aos/state/");

            Directory.CreateDirectory(Path.Combine(aosPath, "spec", "project"));
            touchedFiles.Add(".aos/spec/project/");

            Directory.CreateDirectory(Path.Combine(aosPath, "spec", "roadmap"));
            touchedFiles.Add(".aos/spec/roadmap/");

            Directory.CreateDirectory(Path.Combine(aosPath, "spec", "milestones"));
            touchedFiles.Add(".aos/spec/milestones/");

            Directory.CreateDirectory(Path.Combine(aosPath, "spec", "phases"));
            touchedFiles.Add(".aos/spec/phases/");

            Directory.CreateDirectory(Path.Combine(aosPath, "spec", "tasks"));
            touchedFiles.Add(".aos/spec/tasks/");

            Directory.CreateDirectory(Path.Combine(aosPath, "spec", "issues"));
            touchedFiles.Add(".aos/spec/issues/");

            Directory.CreateDirectory(Path.Combine(aosPath, "spec", "uat"));
            touchedFiles.Add(".aos/spec/uat/");

            Directory.CreateDirectory(Path.Combine(aosPath, "evidence", "runs"));
            touchedFiles.Add(".aos/evidence/runs/");

            var state = new
            {
                status = "initialized",
                cursor = new { },
                version = "1.0"
            };

            var stateJson = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
            var stateFilePath = Path.Combine(aosPath, "state", "state.json");
            System.IO.File.WriteAllText(stateFilePath, stateJson);
            touchedFiles.Add(".aos/state/state.json");

            // Update safety rails with touched files
            SafetyRails.TouchedFiles = touchedFiles;
            SafetyRails.Scope = "workspace initialization";

            // Show toast notification
            this.ToastSuccess($"Workspace initialized with {touchedFiles.Count} items created.");

            return CreateSystemMessage($"Workspace initialized successfully with .aos directory structure. Created {touchedFiles.Count} items.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize workspace");
            this.ToastError($"Failed to initialize workspace: {ex.Message}");
            return CreateSystemMessage($"Failed to initialize workspace: {ex.Message}");
        }
    }

    private ChatMessage GetSpecResponse(string args)
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return CreateSystemMessage("No workspace selected.");
        }

        var specPath = Path.Combine(WorkspacePath, ".aos", "spec");
        if (!Directory.Exists(specPath))
        {
            return CreateSystemMessage("Spec directory does not exist. Run `/init` first.");
        }

        if (string.IsNullOrEmpty(args))
        {
            var specs = new List<string>();
            if (Directory.Exists(Path.Combine(specPath, "project")))
                specs.AddRange(Directory.GetFiles(Path.Combine(specPath, "project"), "*.json").Select(f => $"project/{Path.GetFileName(f)}"));
            if (Directory.Exists(Path.Combine(specPath, "roadmap")))
                specs.AddRange(Directory.GetFiles(Path.Combine(specPath, "roadmap"), "*.json").Select(f => $"roadmap/{Path.GetFileName(f)}"));

            var content = "**Available Specs**\n\n";
            if (specs.Count == 0)
            {
                content += "No spec files found.";
            }
            else
            {
                foreach (var spec in specs)
                {
                    content += $"- {spec}\n";
                }
            }

            return CreateSystemMessage(content);
        }

        return CreateSystemMessage($"Spec command '{args}' processed.");
    }

    private ChatMessage GetRunResponse(string args)
    {
        return CreateSystemMessage($"Run command processed{(string.IsNullOrEmpty(args) ? "" : $" with args: {args}")}. Run tracking will be implemented in a future update.");
    }

    private ChatMessage GetCodebaseResponse()
    {
        return CreateSystemMessage("Codebase analysis will scan the project structure and provide insights. This feature will be implemented in a future update.");
    }

    private ChatMessage GetPackResponse()
    {
        return CreateSystemMessage("Context pack generation will bundle relevant files for AI processing. This feature will be implemented in a future update.");
    }

    private ChatMessage GetCheckpointResponse()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return CreateSystemMessage("No workspace selected.");
        }

        var checkpointId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return CreateSystemMessage($"Checkpoint '{checkpointId}' created. Recovery point saved.");
    }

    private ChatMessage GetUnknownCommandResponse(string cmd)
    {
        return CreateSystemMessage($"Unknown command: '{cmd}'. Type `/help` to see available commands.");
    }

    private ChatMessage CreateSystemMessage(string content)
    {
        return new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Content = content,
            IsUser = false,
            Timestamp = DateTime.Now,
            Type = MessageType.SystemMessage
        };
    }

    private void InitializeCommands()
    {
        AvailableCommands = new List<SlashCommand>
        {
            new() { Command = "/help", Description = "Show available commands" },
            new() { Command = "/status", Description = "Show workspace status" },
            new() { Command = "/validate", Description = "Validate workspace health" },
            new() { Command = "/init", Description = "Initialize .aos workspace" },
            new() { Command = "/spec", Description = "List or manage specs" },
            new() { Command = "/run", Description = "Start or manage runs" },
            new() { Command = "/codebase", Description = "Analyze codebase" },
            new() { Command = "/pack", Description = "Generate context pack" },
            new() { Command = "/checkpoint", Description = "Create recovery checkpoint" }
        };
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

    private void LoadMessageHistory()
    {
        try
        {
            var historyPath = GetMessageHistoryPath();
            var legacyHistoryPath = GetLegacyMessageHistoryPath();

            // Check for new history file first
            if (System.IO.File.Exists(historyPath))
            {
                var json = System.IO.File.ReadAllText(historyPath);
                var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json);
                if (messages != null)
                {
                    Messages = messages;
                }
            }
            // If no new history, check for legacy history and migrate
            else if (System.IO.File.Exists(legacyHistoryPath))
            {
                _logger.LogInformation("Migrating legacy command history from {LegacyPath} to {NewPath}", legacyHistoryPath, historyPath);

                var json = System.IO.File.ReadAllText(legacyHistoryPath);
                var messages = JsonSerializer.Deserialize<List<ChatMessage>>(json);
                if (messages != null)
                {
                    Messages = messages;
                    // Save to new location (migration)
                    SaveMessageHistory();
                    _logger.LogInformation("Successfully migrated {Count} messages to new history file", messages.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load message history");
        }
    }

    private void SaveMessageHistory()
    {
        try
        {
            var historyPath = GetMessageHistoryPath();
            var dir = Path.GetDirectoryName(historyPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(Messages, options);
            System.IO.File.WriteAllText(historyPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save message history");
        }
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }

    private string GetMessageHistoryPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "orchestrator-history.json");
    }

    private string GetLegacyMessageHistoryPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "command-history.json");
    }

    private void LoadSafetyRails()
    {
        try
        {
            var safetyRailsPath = GetSafetyRailsPath();
            if (System.IO.File.Exists(safetyRailsPath))
            {
                var json = System.IO.File.ReadAllText(safetyRailsPath);
                var rails = JsonSerializer.Deserialize<SafetyRails>(json);
                if (rails != null)
                {
                    SafetyRails = rails;
                }
            }
            else
            {
                // Initialize default safety rails
                SafetyRails = new SafetyRails
                {
                    MaxFilesPerOperation = 100,
                    AllowedExtensions = new List<string> { ".cs", ".json", ".md", ".cshtml", ".css", ".js", ".html", ".xml", ".yml", ".yaml" },
                    Scope = "default"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load safety rails configuration");
        }
    }

    private void LoadEvidenceFiles()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        try
        {
            var evidencePath = Path.Combine(WorkspacePath, ".aos", "evidence", "runs");
            if (!Directory.Exists(evidencePath))
            {
                return;
            }

            var files = new List<EvidenceFile>();
            var indexPath = Path.Combine(evidencePath, "index.json");

            if (System.IO.File.Exists(indexPath))
            {
                var json = System.IO.File.ReadAllText(indexPath);
                var index = JsonSerializer.Deserialize<JsonElement>(json);

                if (index.TryGetProperty("items", out var itemsProp) && itemsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in itemsProp.EnumerateArray().Take(10)) // Limit to last 10
                    {
                        var runId = GetStringProperty(item, "runId") ?? GetStringProperty(item, "id");
                        if (!string.IsNullOrEmpty(runId))
                        {
                            files.Add(new EvidenceFile
                            {
                                Name = $"Run {runId}",
                                Path = $"~/Runs/Details/{runId}",
                                Type = "run",
                                CreatedAt = GetStringProperty(item, "startedAt")
                            });
                        }
                    }
                }
            }

            // Also check for individual evidence files
            foreach (var file in Directory.GetFiles(evidencePath, "*.json").Take(5))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.Equals("index.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                files.Add(new EvidenceFile
                {
                    Name = fileName,
                    Path = $"~/evidence/runs/{fileName}",
                    Type = "evidence",
                    CreatedAt = System.IO.File.GetCreationTime(file).ToString("yyyy-MM-dd HH:mm")
                });
            }

            EvidenceFiles = files;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load evidence files");
        }
    }

    private string GetSafetyRailsPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "safety-rails.json");
    }

    private WorkspaceState? LoadState(string aosPath)
    {
        var statePath = Path.Combine(aosPath, "state", "state.json");
        if (!System.IO.File.Exists(statePath))
        {
            return null;
        }

        try
        {
            var json = System.IO.File.ReadAllText(statePath);
            var stateDoc = JsonSerializer.Deserialize<JsonElement>(json);

            return new WorkspaceState
            {
                Status = GetStringProperty(stateDoc, "status") ?? "unknown",
                MilestoneId = GetNestedStringProperty(stateDoc, "cursor", "milestoneId"),
                PhaseId = GetNestedStringProperty(stateDoc, "cursor", "phaseId"),
                TaskId = GetNestedStringProperty(stateDoc, "cursor", "taskId"),
                StepId = GetNestedStringProperty(stateDoc, "cursor", "stepId")
            };
        }
        catch
        {
            return null;
        }
    }

    private string RenderValidationReport(ValidationStatus overallStatus, List<ValidationItem> items)
    {
        var statusIcon = overallStatus switch
        {
            ValidationStatus.Success => "✅",
            ValidationStatus.Warning => "⚠️",
            ValidationStatus.Error => "❌",
            _ => "ℹ️"
        };

        var statusClass = overallStatus.ToString().ToLowerInvariant();

        var html = $"<div class=\"validation-report\">\n";
        html += $"  <div class=\"validation-header\">\n";
        html += $"    <span class=\"validation-item-icon\">{statusIcon}</span>\n";
        html += $"    <span class=\"validation-status {statusClass}\">{overallStatus}</span>\n";
        html += $"  </div>\n";
        html += $"  <div class=\"validation-section\">\n";

        foreach (var item in items)
        {
            var icon = item.Status switch
            {
                ValidationStatus.Success => "✅",
                ValidationStatus.Warning => "⚠️",
                ValidationStatus.Error => "❌",
                _ => "ℹ️"
            };

            var itemClass = item.Status.ToString().ToLowerInvariant();

            html += $"    <div class=\"validation-item {itemClass}\">\n";
            html += $"      <span class=\"validation-item-icon\">{icon}</span>\n";
            html += $"      <span class=\"validation-item-text\">{item.Name}: {item.Message}</span>\n";
            html += $"    </div>\n";
        }

        html += "  </div>\n";
        html += "</div>\n";

        return html;
    }

    public static string HighlightJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;

        // Parse and format JSON
        try
        {
            var element = JsonSerializer.Deserialize<JsonElement>(json);
            json = JsonSerializer.Serialize(element, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            // If parsing fails, return original with basic escaping
        }

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
                highlighted.Append("<span class=\"json-string\">\"");
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

            // Outside strings
            if (c == '{' || c == '}' || c == '[' || c == ']')
            {
                highlighted.Append($"<span class=\"json-bracket\">{c}</span>");
            }
            else if (c == ':')
            {
                // Check if this is a key by looking back
                var prevContent = GetPreviousContent(json, i, 50);
                if (prevContent.TrimEnd().EndsWith("\""))
                {
                    // Insert key closing and colon
                    highlighted.Append("</span><span class=\"json-colon\">:</span>");
                }
                else
                {
                    highlighted.Append("<span class=\"json-colon\">:</span>");
                }
            }
            else if (c == ',')
            {
                highlighted.Append("<span class=\"json-comma\">,</span>");
            }
            else if (char.IsDigit(c) || (c == '-' && i + 1 < json.Length && char.IsDigit(json[i + 1])))
            {
                // Read full number
                var numStart = i;
                while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == 'e' || json[i] == 'E' || json[i] == '-' || json[i] == '+'))
                {
                    i++;
                }
                var number = json.Substring(numStart, i - numStart);
                highlighted.Append($"<span class=\"json-number\">{number}</span>");
                i--; // Adjust for loop increment
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

    private static string GetPreviousContent(string json, int position, int maxLength)
    {
        var start = Math.Max(0, position - maxLength);
        return json.Substring(start, position - start);
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static string? GetNestedStringProperty(JsonElement element, string parentProperty, string childProperty)
    {
        if (element.TryGetProperty(parentProperty, out var parent) && parent.ValueKind == JsonValueKind.Object)
        {
            if (parent.TryGetProperty(childProperty, out var child) && child.ValueKind == JsonValueKind.String)
            {
                return child.GetString();
            }
        }
        return null;
    }

    private ChatMessage ExecuteLocalCommandAsRun(string command, bool success, string localOutput)
    {
        var runId = "unknown";

        if (_runLifecycleManager != null)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));

            var runContext = _runLifecycleManager.StartRunAsync(cts.Token).GetAwaiter().GetResult();
            runId = runContext.RunId;

            var intent = new WorkflowIntent
            {
                InputRaw = command,
                InputNormalized = NormalizeIntentForRun(command),
                CorrelationId = Guid.NewGuid().ToString("N")
            };

            _runLifecycleManager.AttachInputAsync(runContext.RunId, intent, cts.Token).GetAwaiter().GetResult();

            var outputs = new Dictionary<string, object>
            {
                ["command"] = command,
                ["success"] = success,
                ["output"] = localOutput
            };

            _runLifecycleManager.FinishRunAsync(runContext.RunId, success, outputs, cts.Token).GetAwaiter().GetResult();
        }

        var report = RunPreflight();
        var snapshot = TryReadSnapshotJson();
        var content = FormatStructuredTurn(
            rawCommand: command,
            normalizedIntent: NormalizeIntentForDisplay(command),
            preflight: report,
            routingDecision: new RoutingDecision("Local", "Local command handled by UI"),
            runId: runId,
            outputs: GetDefaultOutputsForRun(runId),
            stateSnapshotJson: snapshot,
            nextAction: ChooseNextAction(report),
            localOutput: localOutput
        );

        return new ChatMessage
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Content = content,
            IsUser = false,
            Timestamp = DateTime.Now,
            Type = success ? MessageType.CommandResult : MessageType.SystemMessage,
            HasStructuredData = true
        };
    }

    private string GetHelpText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Available commands:");
        sb.AppendLine();
        foreach (var cmd in AvailableCommands)
        {
            sb.AppendLine($"- {cmd.Command} - {cmd.Description}");
        }
        return sb.ToString().TrimEnd();
    }

    private string GetStatusText()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return "No workspace selected. Please select a workspace first using the Workspace page.";
        }

        var aosPath = Path.Combine(WorkspacePath, ".aos");
        if (!Directory.Exists(aosPath))
        {
            return "Selected workspace does not have a valid .aos directory. Run `/init` to initialize.";
        }

        var state = LoadState(aosPath);
        var hasIssues = Directory.Exists(Path.Combine(aosPath, "spec", "issues"));
        var issueCount = hasIssues ? Directory.GetFiles(Path.Combine(aosPath, "spec", "issues"), "*.json").Length : 0;

        var sb = new StringBuilder();
        sb.AppendLine("Workspace Status");
        sb.AppendLine();
        sb.AppendLine($"- Path: {WorkspacePath}");
        sb.AppendLine($"- Status: {(state != null ? state.Status : "unknown")}");
        sb.AppendLine($"- Cursor: {(state != null ? state.GetCursorDisplay() : "Not set")}");
        sb.AppendLine($"- Open Issues: {issueCount}");
        return sb.ToString().TrimEnd();
    }

    private string GetValidateText()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return "No workspace selected. Please select a workspace first.";
        }

        var report = RunPreflight();
        if (report.Issues.Count == 0)
        {
            return "Workspace validation: OK";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Workspace validation: {report.Issues.Count} issue(s)");
        foreach (var issue in report.Issues.Take(20))
        {
            sb.AppendLine($"- {(issue.Layer ?? "unknown")}: {issue.ContractPath} - {issue.Message}");
        }
        return sb.ToString().TrimEnd();
    }

    private string GetInitText()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return "No workspace selected. Please select a workspace first.";
        }

        var result = AosWorkspaceBootstrapper.EnsureInitialized(WorkspacePath);
        return $"{result.Outcome}: {result.AosRootPath}";
    }

    private PreflightReport RunPreflight()
    {
        try
        {
            var rootPath = _workspace?.RepositoryRootPath ?? WorkspacePath;
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return new PreflightReport(
                    WorkspaceRootPath: "(unknown)",
                    AosRootPath: ".aos",
                    Issues: new List<PreflightIssue> { new(null, ".aos/", "Workspace path is not available.") }
                );
            }

            var report = AosWorkspaceValidator.Validate(
                rootPath,
                new[] { AosWorkspaceLayer.Spec, AosWorkspaceLayer.State, AosWorkspaceLayer.Evidence }
            );

            var issues = report.Issues
                .Select(i => new PreflightIssue(
                    Layer: i.Layer?.ToString(),
                    ContractPath: i.ContractPath,
                    Message: i.Message
                ))
                .ToList();

            return new PreflightReport(
                WorkspaceRootPath: rootPath,
                AosRootPath: report.AosRootPath,
                Issues: issues
            );
        }
        catch (Exception ex)
        {
            return new PreflightReport(
                WorkspaceRootPath: _workspace?.RepositoryRootPath ?? WorkspacePath ?? "(unknown)",
                AosRootPath: Path.Combine(_workspace?.RepositoryRootPath ?? WorkspacePath ?? "", ".aos"),
                Issues: new List<PreflightIssue> { new(null, ".aos/", ex.Message) }
            );
        }
    }

    private string? TryReadSnapshotJson()
    {
        try
        {
            if (_stateStore == null)
            {
                return null;
            }

            var snapshot = _stateStore.ReadSnapshot();
            return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return null;
        }
    }

    private static string NormalizeIntentForRun(string raw)
    {
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("/", StringComparison.Ordinal))
        {
            return trimmed;
        }

        if (trimmed.StartsWith("run ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("plan ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("help", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("status", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        return $"run {trimmed}";
    }

    private static string NormalizeIntentForDisplay(string raw) => NormalizeIntentForRun(raw);

    private static RoutingDecision GetRoutingDecision(OrchestratorResult result)
    {
        var targetPhase = result.Artifacts?.TryGetValue("targetPhase", out var tp) == true
            ? tp?.ToString()
            : result.FinalPhase;

        var reason = result.Artifacts?.TryGetValue("reason", out var r) == true
            ? r?.ToString()
            : null;

        return new RoutingDecision(targetPhase ?? "unknown", reason ?? "(no reason)");
    }

    private static List<string> GetDefaultOutputsForRun(string? runId)
    {
        if (string.IsNullOrWhiteSpace(runId))
        {
            return new List<string>();
        }

        return new List<string>
        {
            $".aos/evidence/runs/{runId}/run.json",
            $".aos/evidence/runs/{runId}/input.json",
            $".aos/evidence/runs/{runId}/commands.json",
            $".aos/evidence/runs/{runId}/summary.json"
        };
    }

    private static string ChooseNextAction(PreflightReport preflight)
    {
        if (preflight.Issues.Any(i => string.Equals(i.ContractPath, ".aos/", StringComparison.Ordinal)))
        {
            return "/init";
        }

        if (preflight.Issues.Count > 0)
        {
            return "/validate";
        }

        return "/status";
    }

    private static string FormatStructuredTurn(
        string rawCommand,
        string normalizedIntent,
        PreflightReport preflight,
        RoutingDecision routingDecision,
        string? runId,
        List<string> outputs,
        string? stateSnapshotJson,
        string nextAction,
        string? localOutput = null)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"**RUN:** `{runId ?? "unknown"}`");
        sb.AppendLine();

        sb.AppendLine("A) Interpretation");
        sb.AppendLine($"- raw: `{rawCommand}`");
        sb.AppendLine($"- normalized: `{normalizedIntent}`");
        sb.AppendLine();

        sb.AppendLine("B) Pre-flight");
        sb.AppendLine($"- workspace: `{preflight.WorkspaceRootPath}`");
        sb.AppendLine($"- aos: `{preflight.AosRootPath}`");
        sb.AppendLine("- layers: `spec`, `state`, `evidence`");
        sb.AppendLine($"- issues: `{preflight.Issues.Count}`");
        if (preflight.Issues.Count > 0)
        {
            foreach (var issue in preflight.Issues.Take(10))
            {
                sb.AppendLine($"  - {(issue.Layer ?? "unknown")}: `{issue.ContractPath}` - {issue.Message}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("C) Routing decision");
        sb.AppendLine($"- targetPhase: `{routingDecision.TargetPhase}`");
        sb.AppendLine($"- reason: {routingDecision.Reason}");
        sb.AppendLine();

        sb.AppendLine("D) Outputs (exact paths)");
        if (outputs.Count == 0)
        {
            sb.AppendLine("- (none)");
        }
        else
        {
            foreach (var path in outputs)
            {
                sb.AppendLine($"- `{path}`");
            }
        }
        sb.AppendLine();

        sb.AppendLine("E) State (.aos/state/state.json cursor snapshot)");
        if (string.IsNullOrWhiteSpace(stateSnapshotJson))
        {
            sb.AppendLine("- (unavailable)");
        }
        else
        {
            sb.AppendLine("```json");
            sb.AppendLine(stateSnapshotJson);
            sb.AppendLine("```");
        }
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(localOutput))
        {
            sb.AppendLine("(Local output)");
            sb.AppendLine("```text");
            sb.AppendLine(localOutput);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("F) Next");
        sb.AppendLine($"- `{nextAction}`");

        return sb.ToString().TrimEnd();
    }

    private sealed record RoutingDecision(string TargetPhase, string Reason);
    private sealed record PreflightIssue(string? Layer, string ContractPath, string Message);
    private sealed record PreflightReport(string WorkspaceRootPath, string AosRootPath, List<PreflightIssue> Issues);
}

public class ChatMessage
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsUser { get; set; }
    public DateTime Timestamp { get; set; }
    public MessageType Type { get; set; }
    public bool HasStructuredData { get; set; }
    public string? HtmlContent { get; set; }
    public bool HasAttachments => Attachments.Count > 0;
    public List<MessageAttachment> Attachments { get; set; } = new();

    public string GetMessageClass()
    {
        return IsUser ? "message-user" : "message-system";
    }

    public string FormattedContent
    {
        get
        {
            if (!string.IsNullOrEmpty(HtmlContent))
                return HtmlContent;
            return Content.Replace("\n", "<br/>");
        }
    }
}

public class SlashCommand
{
    public string Command { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class MessageAttachment
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Icon { get; set; } = "📄";
}

public enum MessageType
{
    UserInput,
    SystemMessage,
    CommandResult
}

public class WorkspaceState
{
    public string Status { get; set; } = "unknown";
    public string? MilestoneId { get; set; }
    public string? PhaseId { get; set; }
    public string? TaskId { get; set; }
    public string? StepId { get; set; }

    public string GetCursorDisplay()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(MilestoneId)) parts.Add(MilestoneId);
        if (!string.IsNullOrEmpty(PhaseId)) parts.Add(PhaseId);
        if (!string.IsNullOrEmpty(TaskId)) parts.Add(TaskId);
        if (!string.IsNullOrEmpty(StepId)) parts.Add(StepId);
        return parts.Count > 0 ? string.Join(" / ", parts) : "No cursor set";
    }
}

public class WorkspaceConfig
{
    public string? SelectedWorkspacePath { get; set; }
}

public class ValidationItem
{
    public string Name { get; set; } = string.Empty;
    public ValidationStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}

public enum ValidationStatus
{
    Success,
    Warning,
    Error,
    Info
}

public class SafetyRails
{
    public int MaxFilesPerOperation { get; set; } = 100;
    public List<string> AllowedExtensions { get; set; } = new();
    public List<string> TouchedFiles { get; set; } = new();
    public string Scope { get; set; } = string.Empty;
    public string LastCommand { get; set; } = string.Empty;
    public DateTime? LastUpdated { get; set; }
}

public class EvidenceFile
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? CreatedAt { get; set; }
    public long? Size { get; set; }
}
