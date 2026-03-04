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

    public async Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default)
        => await db.Messages.FindAsync([messageId], ct);

    public async Task UpdateMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        db.Messages.Update(message);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<ChatMessage>> GetMessagesByConversationAsync(Guid conversationId, CancellationToken ct = default)
        => await db.Messages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<ChatMessage> Messages, int TotalCount)> SearchMessagesAsync(
        Guid conversationId, string query, int page, int pageSize, CancellationToken ct = default)
    {
        var baseQuery = db.Messages
            .Where(m => m.ConversationId == conversationId)
            .Where(m => EF.Functions.ILike(m.Content, $"%{query}%"));

        var totalCount = await baseQuery.CountAsync(ct);

        var messages = await baseQuery
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (messages, totalCount);
    }
}
