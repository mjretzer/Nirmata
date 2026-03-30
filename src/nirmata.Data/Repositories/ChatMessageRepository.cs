using nirmata.Data.Context;
using nirmata.Data.Entities.Chat;
using Microsoft.EntityFrameworkCore;

namespace nirmata.Data.Repositories;

public sealed class ChatMessageRepository : IChatMessageRepository
{
    private readonly nirmataDbContext _dbContext;

    public ChatMessageRepository(nirmataDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<ChatMessage>> GetByWorkspaceIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.Set<ChatMessage>()
            .Where(m => m.WorkspaceId == workspaceId)
            .ToListAsync(cancellationToken);
        return messages.OrderBy(m => m.Timestamp).ToList();
    }

    public void Add(ChatMessage message)
    {
        _dbContext.Set<ChatMessage>().Add(message);
    }

    public async Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
