using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Ports;
using FairBank.Chat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Chat.Infrastructure.Persistence.Repositories;

public sealed class ChatRepository(ChatDbContext db) : IChatRepository
{
    public async Task SaveMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        db.Messages.Add(message);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesByConversationAsync(Guid conversationId, CancellationToken ct = default)
        => await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);
}
