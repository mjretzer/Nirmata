using Gmsd.Aos.Contracts.State;

namespace Gmsd.Aos.Public;

/// <summary>
/// Public contract for persisting and retrieving confirmation state.
/// </summary>
public interface IConfirmationStateStore
{
    /// <summary>
    /// Gets all confirmations (pending and resolved).
    /// </summary>
    IReadOnlyList<ConfirmationState> GetAllConfirmations();

    /// <summary>
    /// Gets pending confirmations that have not been resolved.
    /// </summary>
    IReadOnlyList<ConfirmationState> GetPendingConfirmations();

    /// <summary>
    /// Gets a specific confirmation by ID.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <returns>The confirmation state, or null if not found.</returns>
    ConfirmationState? GetConfirmation(string confirmationId);

    /// <summary>
    /// Saves a confirmation state. Creates if new, updates if existing.
    /// </summary>
    /// <param name="confirmation">The confirmation state to save.</param>
    void SaveConfirmation(ConfirmationState confirmation);

    /// <summary>
    /// Marks a confirmation as accepted.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <returns>True if found and updated, false otherwise.</returns>
    bool AcceptConfirmation(string confirmationId);

    /// <summary>
    /// Marks a confirmation as rejected.
    /// </summary>
    /// <param name="confirmationId">The confirmation ID.</param>
    /// <param name="userMessage">Optional user message explaining rejection.</param>
    /// <returns>True if found and updated, false otherwise.</returns>
    bool RejectConfirmation(string confirmationId, string? userMessage = null);

    /// <summary>
    /// Marks expired pending confirmations as timed out.
    /// </summary>
    /// <param name="cancellationReason">The reason for cancellation.</param>
    /// <returns>List of confirmation IDs that were timed out.</returns>
    IReadOnlyList<string> CleanupExpiredConfirmations(string cancellationReason = "timeout");

    /// <summary>
    /// Removes a confirmation from storage (used for cleanup after completion).
    /// </summary>
    /// <param name="confirmationId">The confirmation ID to remove.</param>
    /// <returns>True if found and removed, false otherwise.</returns>
    bool RemoveConfirmation(string confirmationId);

    /// <summary>
    /// Checks if a confirmation with the same action already exists (duplicate detection).
    /// </summary>
    /// <param name="confirmationKey">The confirmation key to check.</param>
    /// <returns>True if a pending confirmation with this key exists.</returns>
    bool HasPendingConfirmationWithKey(string confirmationKey);

    /// <summary>
    /// Gets an existing pending confirmation by its key (for duplicate detection).
    /// </summary>
    /// <param name="confirmationKey">The confirmation key.</param>
    /// <returns>The existing confirmation state, or null if not found.</returns>
    ConfirmationState? GetPendingConfirmationByKey(string confirmationKey);
}
