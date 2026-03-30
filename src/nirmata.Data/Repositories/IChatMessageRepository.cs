using nirmata.Data.Entities.Chat;

namespace nirmata.Data.Repositories;

public interface IChatMessageRepository
{
    /// <summary>Returns all messages for a workspace, oldest-first.</summary>
    Task<List<ChatMessage>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    void Add(ChatMessage message);

    Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);
}
