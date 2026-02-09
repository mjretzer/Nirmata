using System.Text.Json;
using Gmsd.Aos.Contracts.State;
using Gmsd.Aos.Public;
using Gmsd.Agents.Execution.Continuity;
using Gmsd.Web.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Gmsd.Web.Pages.Checkpoints;

/// <summary>
/// PageModel for the Pause/Resume & Checkpoints dashboard.
/// Provides checkpoint management, lock management, and pause/resume operations.
/// </summary>
public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IConfiguration _configuration;
    private readonly ICheckpointManager? _checkpointManager;
    private readonly ILockManager? _lockManager;
    private readonly IPauseResumeManager? _pauseResumeManager;

    public IndexModel(
        ILogger<IndexModel> logger,
        IConfiguration configuration,
        ICheckpointManager? checkpointManager = null,
        ILockManager? lockManager = null,
        IPauseResumeManager? pauseResumeManager = null)
    {
        _logger = logger;
        _configuration = configuration;
        _checkpointManager = checkpointManager;
        _lockManager = lockManager;
        _pauseResumeManager = pauseResumeManager;
    }

    public string? WorkspacePath { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    // Lock status
    public LockStatus? LockStatus { get; set; }
    public bool LockExists { get; set; }

    // Checkpoints
    public List<CheckpointViewModel> Checkpoints { get; set; } = new();

    // Handoff state
    public HandoffState? Handoff { get; set; }
    public bool HandoffExists { get; set; }

    // Form properties
    [BindProperty]
    public string? CheckpointDescription { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ViewCheckpointId { get; set; }

    [BindProperty]
    public string? RestoreCheckpointId { get; set; }

    [BindProperty]
    public string? PauseReason { get; set; }

    public async Task OnGetAsync()
    {
        LoadWorkspace();

        if (!string.IsNullOrEmpty(WorkspacePath))
        {
            await LoadLockStatusAsync();
            await LoadCheckpointsAsync();
            await LoadHandoffAsync();

            if (!string.IsNullOrEmpty(ViewCheckpointId))
            {
                LoadCheckpointDetails();
            }
        }
    }

    public async Task<IActionResult> OnPostCreateCheckpointAsync()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            this.ToastError("No workspace selected");
            return RedirectToPage();
        }

        // Check for lock conflicts
        await LoadLockStatusAsync();
        if (LockExists && LockStatus?.Holder != null)
        {
            this.ToastLockConflict(LockStatus.Holder.ProcessName, "create checkpoint");
            return RedirectToPage();
        }

        try
        {
            if (_checkpointManager != null)
            {
                var checkpointId = _checkpointManager.CreateCheckpoint(CheckpointDescription);
                this.ToastSuccess($"Checkpoint {checkpointId} created successfully.");
                _logger.LogInformation("Checkpoint created: {CheckpointId}", checkpointId);
            }
            else
            {
                // Fallback: create checkpoint directory and metadata manually
                var checkpointId = await CreateCheckpointManualAsync(CheckpointDescription);
                this.ToastSuccess($"Checkpoint {checkpointId} created.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create checkpoint");
            this.ToastError($"Failed to create checkpoint: {ex.Message}");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRestoreCheckpointAsync()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            this.ToastError("No workspace selected");
            return RedirectToPage();
        }

        if (string.IsNullOrEmpty(RestoreCheckpointId))
        {
            this.ToastError("No checkpoint selected for restore");
            return RedirectToPage();
        }

        // Check for lock conflicts
        await LoadLockStatusAsync();
        if (LockExists && LockStatus?.Holder != null)
        {
            this.ToastLockConflict(LockStatus.Holder.ProcessName, "restore checkpoint");
            return RedirectToPage();
        }

        try
        {
            if (_checkpointManager != null)
            {
                _checkpointManager.RestoreCheckpoint(RestoreCheckpointId);
                this.ToastSuccess($"Checkpoint {RestoreCheckpointId} restored successfully.");
                _logger.LogInformation("Checkpoint restored: {CheckpointId}", RestoreCheckpointId);
            }
            else
            {
                this.ToastError("Checkpoint manager not available.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore checkpoint {CheckpointId}", RestoreCheckpointId);
            this.ToastError($"Failed to restore checkpoint: {ex.Message}");
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostPauseAsync()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        try
        {
            if (_pauseResumeManager != null)
            {
                var handoffMetadata = await _pauseResumeManager.PauseAsync(PauseReason);
                SuccessMessage = $"Paused successfully. Handoff created at {handoffMetadata.HandoffPath}";
                _logger.LogInformation("Pause executed. Handoff: {HandoffPath}", handoffMetadata.HandoffPath);
            }
            else
            {
                // Fallback: create handoff.json manually
                await CreateHandoffManualAsync(PauseReason);
                SuccessMessage = "Pause executed (manual). Handoff.json created.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause execution");
            ErrorMessage = $"Failed to pause: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResumeAsync()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        try
        {
            if (_pauseResumeManager != null)
            {
                var result = await _pauseResumeManager.ResumeAsync();
                SuccessMessage = $"Resumed successfully. New run ID: {result.RunId}";
                _logger.LogInformation("Resume executed. New run: {RunId}", result.RunId);
            }
            else
            {
                ErrorMessage = "Pause/Resume manager not available.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resume execution");
            ErrorMessage = $"Failed to resume: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostValidateHandoffAsync()
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return RedirectToPage(new { ErrorMessage = "No workspace selected" });
        }

        try
        {
            if (_pauseResumeManager != null)
            {
                var validation = await _pauseResumeManager.ValidateHandoffAsync();
                if (validation.IsValid)
                {
                    SuccessMessage = "Handoff is valid and can be resumed.";
                }
                else
                {
                    ErrorMessage = $"Handoff validation failed: {string.Join(", ", validation.Errors)}";
                }
            }
            else
            {
                // Basic validation: check if handoff.json exists and is valid JSON
                var handoffPath = Path.Combine(WorkspacePath, ".aos", "state", "handoff.json");
                if (!System.IO.File.Exists(handoffPath))
                {
                    ErrorMessage = "Handoff file does not exist.";
                }
                else
                {
                    var json = await System.IO.File.ReadAllTextAsync(handoffPath);
                    try
                    {
                        JsonSerializer.Deserialize<JsonElement>(json);
                        SuccessMessage = "Handoff file exists and is valid JSON.";
                    }
                    catch
                    {
                        ErrorMessage = "Handoff file exists but contains invalid JSON.";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate handoff");
            ErrorMessage = $"Failed to validate handoff: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReleaseLockAsync(bool force = false)
    {
        LoadWorkspace();
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            this.ToastError("No workspace selected");
            return RedirectToPage();
        }

        try
        {
            if (_lockManager != null)
            {
                var released = _lockManager.Release(force);
                if (released)
                {
                    this.ToastSuccess(force ? "Lock force-released." : "Lock released successfully.");
                    _logger.LogInformation("Lock released (force={Force})", force);
                }
                else
                {
                    this.ToastError("Failed to release lock (not held or validation failed).");
                }
            }
            else
            {
                // Fallback: remove lock file manually
                var lockPath = Path.Combine(WorkspacePath, ".aos", "locks", "workspace.lock.json");
                if (System.IO.File.Exists(lockPath))
                {
                    System.IO.File.Delete(lockPath);
                    this.ToastSuccess("Lock file removed (manual).");
                }
                else
                {
                    this.ToastWarning("No lock file exists.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release lock");
            this.ToastError($"Failed to release lock: {ex.Message}");
        }

        return RedirectToPage();
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

    private async Task LoadLockStatusAsync()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        try
        {
            if (_lockManager != null)
            {
                LockStatus = _lockManager.GetStatus();
                LockExists = LockStatus.IsLocked;
            }
            else
            {
                // Fallback: check lock file directly
                var lockPath = Path.Combine(WorkspacePath, ".aos", "locks", "workspace.lock.json");
                LockExists = System.IO.File.Exists(lockPath);

                if (LockExists)
                {
                    try
                    {
                        var json = await System.IO.File.ReadAllTextAsync(lockPath);
                        var lockData = JsonSerializer.Deserialize<JsonElement>(json);

                        var processId = lockData.TryGetProperty("processId", out var pid) ? pid.GetInt32() : 0;
                        var processName = lockData.TryGetProperty("processName", out var pname) ? pname.GetString() : "unknown";
                        var startedAt = lockData.TryGetProperty("startedAtUtc", out var started) ? started.GetString() : null;
                        var machineName = lockData.TryGetProperty("machineName", out var machine) ? machine.GetString() : "unknown";

                        DateTimeOffset? startedAtOffset = null;
                        if (!string.IsNullOrEmpty(startedAt) && DateTimeOffset.TryParse(startedAt, out var parsed))
                        {
                            startedAtOffset = parsed;
                        }

                        LockStatus = new LockStatus(
                            true,
                            new LockHolderInfo(processId, processName ?? "unknown", startedAtOffset ?? DateTimeOffset.UtcNow, machineName ?? "unknown")
                        );
                    }
                    catch
                    {
                        LockStatus = new LockStatus(true, null);
                    }
                }
                else
                {
                    LockStatus = new LockStatus(false, null);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading lock status");
        }
    }

    private async Task LoadCheckpointsAsync()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        try
        {
            if (_checkpointManager != null)
            {
                var checkpoints = _checkpointManager.ListCheckpoints();
                Checkpoints = checkpoints.Select(c => new CheckpointViewModel
                {
                    CheckpointId = c.CheckpointId,
                    CreatedAtUtc = c.CreatedAtUtc,
                    Description = c.Description,
                    StateSnapshotPath = c.StateSnapshotPath
                }).ToList();
            }
            else
            {
                // Fallback: list checkpoints directory manually
                var checkpointsPath = Path.Combine(WorkspacePath, ".aos", "state", "checkpoints");
                if (Directory.Exists(checkpointsPath))
                {
                    var checkpointDirs = Directory.GetDirectories(checkpointsPath);
                    foreach (var dir in checkpointDirs.OrderBy(d => d))
                    {
                        var checkpointId = Path.GetFileName(dir);
                        var metadataPath = Path.Combine(dir, "metadata.json");
                        var snapshotPath = Path.Combine(dir, "state.json");

                        string? description = null;
                        DateTimeOffset createdAt = DateTimeOffset.MinValue;

                        if (System.IO.File.Exists(metadataPath))
                        {
                            try
                            {
                                var json = await System.IO.File.ReadAllTextAsync(metadataPath);
                                var metadata = JsonSerializer.Deserialize<JsonElement>(json);
                                description = metadata.TryGetProperty("description", out var desc) ? desc.GetString() : null;
                                if (metadata.TryGetProperty("createdAtUtc", out var created) &&
                                    DateTimeOffset.TryParse(created.GetString(), out var parsed))
                                {
                                    createdAt = parsed;
                                }
                            }
                            catch { }
                        }

                        Checkpoints.Add(new CheckpointViewModel
                        {
                            CheckpointId = checkpointId,
                            CreatedAtUtc = createdAt,
                            Description = description,
                            StateSnapshotPath = snapshotPath
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading checkpoints");
        }
    }

    private async Task LoadHandoffAsync()
    {
        if (string.IsNullOrEmpty(WorkspacePath))
        {
            return;
        }

        var handoffPath = Path.Combine(WorkspacePath, ".aos", "state", "handoff.json");
        HandoffExists = System.IO.File.Exists(handoffPath);

        if (HandoffExists)
        {
            try
            {
                var json = await System.IO.File.ReadAllTextAsync(handoffPath);
                Handoff = JsonSerializer.Deserialize<HandoffState>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize handoff.json");
            }
        }
    }

    private void LoadCheckpointDetails()
    {
        // This method is called when ViewCheckpointId is specified
        // The checkpoint details will be displayed in the view
    }

    private async Task<string> CreateCheckpointManualAsync(string? description)
    {
        var checkpointsPath = Path.Combine(WorkspacePath!, ".aos", "state", "checkpoints");
        Directory.CreateDirectory(checkpointsPath);

        // Find next checkpoint ID
        var existingDirs = Directory.GetDirectories(checkpointsPath);
        var nextId = existingDirs.Length > 0
            ? existingDirs.Select(d => Path.GetFileName(d))
                         .Where(n => n.StartsWith("CHK-"))
                         .Select(n => int.TryParse(n.Substring(4), out var id) ? id : 0)
                         .Max() + 1
            : 1;

        var checkpointId = $"CHK-{nextId:D4}";
        var checkpointDir = Path.Combine(checkpointsPath, checkpointId);
        Directory.CreateDirectory(checkpointDir);

        // Copy state.json
        var statePath = Path.Combine(WorkspacePath!, ".aos", "state", "state.json");
        if (System.IO.File.Exists(statePath))
        {
            System.IO.File.Copy(statePath, Path.Combine(checkpointDir, "state.json"), overwrite: true);
        }

        // Create metadata
        var metadata = new
        {
            checkpointId,
            createdAtUtc = DateTimeOffset.UtcNow.ToString("O"),
            description
        };
        var metadataJson = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(Path.Combine(checkpointDir, "metadata.json"), metadataJson);

        return checkpointId;
    }

    private async Task CreateHandoffManualAsync(string? reason)
    {
        var handoffPath = Path.Combine(WorkspacePath!, ".aos", "state", "handoff.json");
        Directory.CreateDirectory(Path.GetDirectoryName(handoffPath)!);

        // Read current state
        var statePath = Path.Combine(WorkspacePath!, ".aos", "state", "state.json");
        StateSnapshot? stateSnapshot = null;
        if (System.IO.File.Exists(statePath))
        {
            var stateJson = await System.IO.File.ReadAllTextAsync(statePath);
            stateSnapshot = JsonSerializer.Deserialize<StateSnapshot>(stateJson);
        }

        var handoff = new
        {
            schemaVersion = "1.0",
            timestamp = DateTimeOffset.UtcNow.ToString("O"),
            sourceRunId = "manual-pause",
            cursor = stateSnapshot?.Cursor ?? new StateCursor(),
            taskContext = new { },
            scope = new { },
            nextCommand = new { },
            reason
        };

        var handoffJson = JsonSerializer.Serialize(handoff, new JsonSerializerOptions { WriteIndented = true });
        await System.IO.File.WriteAllTextAsync(handoffPath, handoffJson);
    }

    private string GetWorkspaceConfigPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "Gmsd", "workspace-config.json");
    }
}

public class CheckpointViewModel
{
    public string CheckpointId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? Description { get; set; }
    public string StateSnapshotPath { get; set; } = string.Empty;
}

internal class WorkspaceConfig
{
    public string? SelectedWorkspacePath { get; set; }
}
