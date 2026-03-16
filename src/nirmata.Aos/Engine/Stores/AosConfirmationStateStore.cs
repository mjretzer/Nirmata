using System.Text.Json;
using System.Text.Json.Serialization;
using nirmata.Aos.Contracts.State;

namespace nirmata.Aos.Engine.Stores;

/// <summary>
/// Stores confirmation state in .aos/state/confirmations.json.
/// </summary>
internal sealed class AosConfirmationStateStore : AosJsonStoreBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public const string ConfirmationsPath = ".aos/state/confirmations.json";

    public AosConfirmationStateStore(string aosRootPath)
        : base(aosRootPath, ".aos/state/")
    {
    }

    /// <summary>
    /// Reads the confirmations document from storage.
    /// </summary>
    private ConfirmationsStateDocument ReadDocument()
    {
        if (!Exists(ConfirmationsPath))
        {
            return new ConfirmationsStateDocument();
        }
        return ReadJson<ConfirmationsStateDocument>(ConfirmationsPath, JsonOptions);
    }

    /// <summary>
    /// Writes the confirmations document to storage.
    /// </summary>
    private void WriteDocument(ConfirmationsStateDocument doc)
    {
        var updated = new ConfirmationsStateDocument
        {
            SchemaVersion = doc.SchemaVersion,
            LastUpdated = DateTimeOffset.UtcNow,
            Confirmations = doc.Confirmations
        };
        WriteJsonOverwrite(ConfirmationsPath, updated, JsonOptions, writeIndented: true);
    }

    /// <summary>
    /// Gets all confirmations (pending and resolved).
    /// </summary>
    public IReadOnlyList<ConfirmationState> GetAllConfirmations()
    {
        var doc = ReadDocument();
        return doc.Confirmations.AsReadOnly();
    }

    /// <summary>
    /// Gets pending confirmations that have not been resolved.
    /// </summary>
    public IReadOnlyList<ConfirmationState> GetPendingConfirmations()
    {
        var doc = ReadDocument();
        return doc.Confirmations
            .Where(c => c.Status == ConfirmationStatus.Pending)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a specific confirmation by ID.
    /// </summary>
    public ConfirmationState? GetConfirmation(string confirmationId)
    {
        var doc = ReadDocument();
        return doc.Confirmations.FirstOrDefault(c => c.Id == confirmationId);
    }

    /// <summary>
    /// Saves a confirmation state. Creates if new, updates if existing.
    /// </summary>
    public void SaveConfirmation(ConfirmationState confirmation)
    {
        var doc = ReadDocument();
        var existingIndex = doc.Confirmations.FindIndex(c => c.Id == confirmation.Id);

        if (existingIndex >= 0)
        {
            doc.Confirmations[existingIndex] = confirmation;
        }
        else
        {
            doc.Confirmations.Add(confirmation);
        }

        WriteDocument(doc);
    }

    /// <summary>
    /// Marks a confirmation as accepted.
    /// </summary>
    public bool AcceptConfirmation(string confirmationId)
    {
        var doc = ReadDocument();
        var confirmation = doc.Confirmations.FirstOrDefault(c => c.Id == confirmationId);

        if (confirmation == null || confirmation.Status != ConfirmationStatus.Pending)
        {
            return false;
        }

        var index = doc.Confirmations.IndexOf(confirmation);
        doc.Confirmations[index] = confirmation with
        {
            Status = ConfirmationStatus.Accepted,
            RespondedAt = DateTimeOffset.UtcNow
        };

        WriteDocument(doc);
        return true;
    }

    /// <summary>
    /// Marks a confirmation as rejected.
    /// </summary>
    public bool RejectConfirmation(string confirmationId, string? userMessage = null)
    {
        var doc = ReadDocument();
        var confirmation = doc.Confirmations.FirstOrDefault(c => c.Id == confirmationId);

        if (confirmation == null || confirmation.Status != ConfirmationStatus.Pending)
        {
            return false;
        }

        var index = doc.Confirmations.IndexOf(confirmation);
        doc.Confirmations[index] = confirmation with
        {
            Status = ConfirmationStatus.Rejected,
            RespondedAt = DateTimeOffset.UtcNow,
            UserMessage = userMessage
        };

        WriteDocument(doc);
        return true;
    }

    /// <summary>
    /// Marks expired pending confirmations as timed out.
    /// </summary>
    public IReadOnlyList<string> CleanupExpiredConfirmations(string cancellationReason = "timeout")
    {
        var doc = ReadDocument();
        var expiredIds = new List<string>();
        var now = DateTimeOffset.UtcNow;

        for (int i = 0; i < doc.Confirmations.Count; i++)
        {
            var confirmation = doc.Confirmations[i];
            if (confirmation.Status == ConfirmationStatus.Pending &&
                confirmation.ExpiresAt.HasValue &&
                now > confirmation.ExpiresAt.Value)
            {
                doc.Confirmations[i] = confirmation with
                {
                    Status = ConfirmationStatus.TimedOut,
                    TimedOutAt = now,
                    CancellationReason = cancellationReason
                };
                expiredIds.Add(confirmation.Id);
            }
        }

        if (expiredIds.Count > 0)
        {
            WriteDocument(doc);
        }

        return expiredIds.AsReadOnly();
    }

    /// <summary>
    /// Removes a confirmation from storage (used for cleanup after completion).
    /// </summary>
    public bool RemoveConfirmation(string confirmationId)
    {
        var doc = ReadDocument();
        var confirmation = doc.Confirmations.FirstOrDefault(c => c.Id == confirmationId);

        if (confirmation == null)
        {
            return false;
        }

        doc.Confirmations.Remove(confirmation);
        WriteDocument(doc);
        return true;
    }

    /// <summary>
    /// Checks if a confirmation with the same action already exists (duplicate detection).
    /// </summary>
    public bool HasPendingConfirmationWithKey(string confirmationKey)
    {
        if (string.IsNullOrEmpty(confirmationKey))
        {
            return false;
        }

        var doc = ReadDocument();
        return doc.Confirmations.Any(c =>
            c.Status == ConfirmationStatus.Pending &&
            c.ConfirmationKey == confirmationKey);
    }

    /// <summary>
    /// Gets an existing pending confirmation by its key (for duplicate detection).
    /// </summary>
    public ConfirmationState? GetPendingConfirmationByKey(string confirmationKey)
    {
        if (string.IsNullOrEmpty(confirmationKey))
        {
            return null;
        }

        var doc = ReadDocument();
        return doc.Confirmations.FirstOrDefault(c =>
            c.Status == ConfirmationStatus.Pending &&
            c.ConfirmationKey == confirmationKey);
    }
}
