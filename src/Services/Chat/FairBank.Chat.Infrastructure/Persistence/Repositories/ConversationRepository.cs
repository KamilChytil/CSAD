using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Enums;
using FairBank.Chat.Domain.Ports;
using FairBank.Chat.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Chat.Infrastructure.Persistence.Repositories;

public sealed class ConversationRepository(ChatDbContext db) : IConversationRepository
{
    public async Task<Conversation> GetOrCreateSupportAsync(Guid clientId, string clientLabel, CancellationToken ct = default)
    {
        var existing = await db.Conversations
            .FirstOrDefaultAsync(c => c.Type == ConversationType.Support && c.ClientOrChildId == clientId, ct);

        if (existing is not null) return existing;

        var created = Conversation.CreateSupport(clientId, clientLabel);
        db.Conversations.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<Conversation> GetOrCreateFamilyAsync(Guid parentId, Guid childId, string childLabel, CancellationToken ct = default)
    {
        var existing = await db.Conversations
            .FirstOrDefaultAsync(c => c.Type == ConversationType.Family
                                   && c.BankerOrParentId == parentId
                                   && c.ClientOrChildId == childId, ct);

        if (existing is not null) return existing;

        var created = Conversation.CreateFamily(parentId, childId, childLabel);
        db.Conversations.Add(created);
        await db.SaveChangesAsync(ct);
        return created;
    }

    public async Task<IEnumerable<Conversation>> GetAllSupportAsync(CancellationToken ct = default)
        => await db.Conversations
            .Where(c => c.Type == ConversationType.Support)
            .OrderBy(c => c.Label)
            .ToListAsync(ct);

    public async Task<IEnumerable<Conversation>> GetFamilyByParentAsync(Guid parentId, CancellationToken ct = default)
        => await db.Conversations
            .Where(c => c.Type == ConversationType.Family && c.BankerOrParentId == parentId)
            .OrderBy(c => c.Label)
            .ToListAsync(ct);

    public async Task<Conversation?> GetFamilyByChildAsync(Guid childId, CancellationToken ct = default)
        => await db.Conversations
            .FirstOrDefaultAsync(c => c.Type == ConversationType.Family && c.ClientOrChildId == childId, ct);

    public async Task<Conversation?> GetSupportByClientAsync(Guid clientId, CancellationToken ct = default)
        => await db.Conversations
            .FirstOrDefaultAsync(c => c.Type == ConversationType.Support && c.ClientOrChildId == clientId, ct);
}
