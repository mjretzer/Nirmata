using System.Text.Json;
using System.Text.Json.Serialization;
using Gmsd.Agents.Execution.ControlPlane;

namespace Gmsd.Agents.Execution.Preflight;

/// <summary>
/// Represents a persisted confirmation state for resumability.
/// </summary>
public sealed class PersistedConfirmation
{
    /// <summary>
    /// Unique identifier for the confirmation.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Current state of the confirmation.
    /// </summary>
    public required string State { get; set; } // "Pending", "Accepted", "Rejected", "Timeout"

    /// <summary>
    /// When the confirmation was requested.
    /// </summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>
    /// Optional timeout duration.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// The phase that will be executed.
    /// </summary>
    public required string Phase { get; init; }

    /// <summary>
    /// Description of the proposed action.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Risk level of the operation.
    /// </summary>
    public required string RiskLevel { get; init; }

    /// <summary>
    /// Affected resources for the operation.
    /// </summary>
    public List<string> AffectedResources { get; init; } = new();

    /// <summary>
    /// Confidence score that triggered the confirmation.
    /// </summary>
    public double Confidence { get; init; }

    /// <summary>
    /// The threshold that was not met (if applicable).
    /// </summary>
    public double? Threshold { get; init; }

    /// <summary>
    /// When the confirmation was responded to (if completed).
    /// </summary>
    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>
    /// Whether the confirmation was accepted (if completed).
    /// </summary>
    public bool? Accepted { get; set; }

    /// <summary>
    /// User message accompanying the response (if any).
    /// </summary>
    public string? UserMessage { get; set; }
}

/// <summary>
/// Container for all persisted confirmations.
/// </summary>
public sealed class ConfirmationStateContainer
{
    /// <summary>
    /// List of all confirmations (pending and completed).
    /// </summary>
    public List<PersistedConfirmation> Confirmations { get; init; } = new();

    /// <summary>
    /// Last updated timestamp.
    /// </summary>
    public DateTimeOffset LastUpdated { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Manages persistence of confirmation state to .aos/state/confirmations.json.
/// </summary>
public sealed class ConfirmationStateStore
{
    private readonly string _stateDirectory;
    private readonly string _stateFilePath;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfirmationStateStore"/> class.
    /// </summary>
    /// <param name="workspaceRoot">The workspace root directory.</param>
    public ConfirmationStateStore(string workspaceRoot)
    {
        _stateDirectory = Path.Combine(workspaceRoot, ".aos", "state");
        _stateFilePath = Path.Combine(_stateDirectory, "confirmations.json");
    }

    /// <summary>
    /// Saves a pending confirmation to the state store.
    /// </summary>
    /// <param name="request">The confirmation request to save.</param>
    /// <param name="proposedAction">The proposed action details.</param>
    /// <param name="riskLevel">The risk level of the operation.</param>
    public void SavePendingConfirmation(ConfirmationRequest request, ProposedAction proposedAction, RiskLevel riskLevel)
    {
        EnsureDirectoryExists();

        lock (_lock)
        {
            var container = LoadContainer();

            // Remove any existing confirmation with the same ID
            container.Confirmations.RemoveAll(c => c.Id == request.Id);

            // Add the new confirmation
            container.Confirmations.Add(new PersistedConfirmation
            {
                Id = request.Id,
                State = "Pending",
                RequestedAt = request.RequestedAt,
                Timeout = request.Timeout,
                Phase = proposedAction.Phase,
                Description = proposedAction.Description,
                RiskLevel = riskLevel.ToString(),
                AffectedResources = proposedAction.AffectedResources.ToList(),
                Confidence = request.Confidence,
                Threshold = request.Threshold
            });

            SaveContainer(container);
        }
    }

    /// <summary>
    /// Updates a confirmation with the user's response.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <param name="accepted">Whether the confirmation was accepted.</param>
    /// <param name="userMessage">Optional user message.</param>
    public void UpdateConfirmationResponse(string confirmationId, bool accepted, string? userMessage = null)
    {
        lock (_lock)
        {
            var container = LoadContainer();
            var confirmation = container.Confirmations.FirstOrDefault(c => c.Id == confirmationId);

            if (confirmation != null)
            {
                confirmation.State = accepted ? "Accepted" : "Rejected";
                confirmation.Accepted = accepted;
                confirmation.RespondedAt = DateTimeOffset.UtcNow;
                confirmation.UserMessage = userMessage;

                SaveContainer(container);
            }
        }
    }

    /// <summary>
    /// Marks a confirmation as timed out.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    public void MarkTimeout(string confirmationId)
    {
        lock (_lock)
        {
            var container = LoadContainer();
            var confirmation = container.Confirmations.FirstOrDefault(c => c.Id == confirmationId);

            if (confirmation != null && confirmation.State == "Pending")
            {
                confirmation.State = "Timeout";
                confirmation.RespondedAt = DateTimeOffset.UtcNow;
                confirmation.Accepted = false;

                SaveContainer(container);
            }
        }
    }

    /// <summary>
    /// Gets all pending confirmations.
    /// </summary>
    /// <returns>List of pending confirmations.</returns>
    public IReadOnlyList<PersistedConfirmation> GetPendingConfirmations()
    {
        lock (_lock)
        {
            var container = LoadContainer();
            return container.Confirmations
                .Where(c => c.State == "Pending")
                .ToList();
        }
    }

    /// <summary>
    /// Gets a specific confirmation by ID.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <returns>The confirmation if found, null otherwise.</returns>
    public PersistedConfirmation? GetConfirmation(string confirmationId)
    {
        lock (_lock)
        {
            var container = LoadContainer();
            return container.Confirmations.FirstOrDefault(c => c.Id == confirmationId);
        }
    }

    /// <summary>
    /// Cleans up completed confirmations older than the specified age.
    /// </summary>
    /// <param name="maxAge">The maximum age of completed confirmations to keep.</param>
    public void CleanupCompletedConfirmations(TimeSpan maxAge)
    {
        lock (_lock)
        {
            var container = LoadContainer();
            var cutoff = DateTimeOffset.UtcNow - maxAge;

            container.Confirmations.RemoveAll(c =>
                c.State != "Pending" && c.RequestedAt < cutoff);

            SaveContainer(container);
        }
    }

    private void EnsureDirectoryExists()
    {
        if (!Directory.Exists(_stateDirectory))
        {
            Directory.CreateDirectory(_stateDirectory);
        }
    }

    private ConfirmationStateContainer LoadContainer()
    {
        if (!File.Exists(_stateFilePath))
        {
            return new ConfirmationStateContainer();
        }

        try
        {
            var json = File.ReadAllText(_stateFilePath);
            var container = JsonSerializer.Deserialize<ConfirmationStateContainer>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            return container ?? new ConfirmationStateContainer();
        }
        catch
        {
            return new ConfirmationStateContainer();
        }
    }

    private void SaveContainer(ConfirmationStateContainer container)
    {
        var json = JsonSerializer.Serialize(new ConfirmationStateContainer
        {
            Confirmations = container.Confirmations,
            LastUpdated = DateTimeOffset.UtcNow
        }, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        File.WriteAllText(_stateFilePath, json);
    }
}
