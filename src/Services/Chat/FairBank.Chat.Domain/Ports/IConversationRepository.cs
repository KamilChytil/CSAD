using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Enums;

namespace FairBank.Chat.Domain.Ports;

public interface IConversationRepository
{
    Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAsync(Conversation conversation, CancellationToken ct = default);

    Task<Conversation> GetOrCreateSupportAsync(Guid clientId, string clientLabel, CancellationToken ct = default);
    Task<Conversation> GetOrCreateFamilyAsync(Guid parentId, Guid childId, string childLabel, CancellationToken ct = default);

    Task<IEnumerable<Conversation>> GetAllSupportAsync(CancellationToken ct = default);
    Task<IEnumerable<Conversation>> GetFamilyByParentAsync(Guid parentId, CancellationToken ct = default);
    Task<Conversation?> GetFamilyByChildAsync(Guid childId, CancellationToken ct = default);
    Task<Conversation?> GetSupportByClientAsync(Guid clientId, CancellationToken ct = default);
    Task<IEnumerable<Conversation>> GetAllSupportForClientAsync(Guid clientId, CancellationToken ct = default);
}
