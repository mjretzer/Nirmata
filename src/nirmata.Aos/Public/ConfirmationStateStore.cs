using nirmata.Aos.Contracts.State;
using nirmata.Aos.Engine.Stores;

namespace nirmata.Aos.Public;

/// <summary>
/// Public confirmation state store implementation backed by the internal engine store.
/// </summary>
public sealed class ConfirmationStateStore : IConfirmationStateStore
{
    private readonly AosConfirmationStateStore _inner;

    private ConfirmationStateStore(string aosRootPath)
    {
        if (string.IsNullOrWhiteSpace(aosRootPath))
        {
            throw new ArgumentException("Missing AOS root path.", nameof(aosRootPath));
        }

        _inner = new AosConfirmationStateStore(aosRootPath);
    }

    /// <summary>
    /// Creates a confirmation state store for an explicit .aos root path.
    /// </summary>
    public static ConfirmationStateStore FromAosRoot(string aosRootPath) => new(aosRootPath);

    /// <summary>
    /// Creates a confirmation state store for a workspace's .aos root.
    /// </summary>
    public static ConfirmationStateStore FromWorkspace(IWorkspace workspace)
    {
        if (workspace is null) throw new ArgumentNullException(nameof(workspace));
        return new ConfirmationStateStore(workspace.AosRootPath);
    }

    public IReadOnlyList<ConfirmationState> GetAllConfirmations() => _inner.GetAllConfirmations();

    public IReadOnlyList<ConfirmationState> GetPendingConfirmations() => _inner.GetPendingConfirmations();

    public ConfirmationState? GetConfirmation(string confirmationId) => _inner.GetConfirmation(confirmationId);

    public void SaveConfirmation(ConfirmationState confirmation) => _inner.SaveConfirmation(confirmation);

    public bool AcceptConfirmation(string confirmationId) => _inner.AcceptConfirmation(confirmationId);

    public bool RejectConfirmation(string confirmationId, string? userMessage = null) =>
        _inner.RejectConfirmation(confirmationId, userMessage);

    public IReadOnlyList<string> CleanupExpiredConfirmations(string cancellationReason = "timeout") =>
        _inner.CleanupExpiredConfirmations(cancellationReason);

    public bool RemoveConfirmation(string confirmationId) => _inner.RemoveConfirmation(confirmationId);

    public bool HasPendingConfirmationWithKey(string confirmationKey) =>
        _inner.HasPendingConfirmationWithKey(confirmationKey);

    public ConfirmationState? GetPendingConfirmationByKey(string confirmationKey) =>
        _inner.GetPendingConfirmationByKey(confirmationKey);
}
