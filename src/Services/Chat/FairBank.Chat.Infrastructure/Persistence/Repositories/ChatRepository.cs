using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Chat.Infrastructure.Persistence.Repositories;

public sealed class ChatRepository(ChatDbContext context) : IChatRepository
{
    public async Task<ChatMessage?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await context.Messages.FindAsync([id], ct);
    }

    public async Task AddAsync(ChatMessage aggregate, CancellationToken ct = default)
    {
        await context.Messages.AddAsync(aggregate, ct);
    }

    public Task UpdateAsync(ChatMessage aggregate, CancellationToken ct = default)
    {
        context.Messages.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesBetweenUsersAsync(Guid user1Id, Guid user2Id, CancellationToken ct = default)
    {
        return await context.Messages
            .Where(m => (m.SenderId == user1Id && m.ReceiverId == user2Id) ||
                        (m.SenderId == user2Id && m.ReceiverId == user1Id))
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);
    }
}
