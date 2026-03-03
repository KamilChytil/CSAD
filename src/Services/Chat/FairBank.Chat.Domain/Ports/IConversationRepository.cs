using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Enums;

namespace FairBank.Chat.Domain.Ports;

public interface IConversationRepository
{
    /// <summary>Returns existing support conversation for client, or creates one.</summary>
    Task<Conversation> GetOrCreateSupportAsync(Guid clientId, string clientLabel, CancellationToken ct = default);

    /// <summary>Returns existing family conversation for parent-child pair, or creates one.</summary>
    Task<Conversation> GetOrCreateFamilyAsync(Guid parentId, Guid childId, string childLabel, CancellationToken ct = default);

    /// <summary>Returns all support conversations (for bankers).</summary>
    Task<IEnumerable<Conversation>> GetAllSupportAsync(CancellationToken ct = default);

    /// <summary>Returns all family conversations where the user is the parent.</summary>
    Task<IEnumerable<Conversation>> GetFamilyByParentAsync(Guid parentId, CancellationToken ct = default);

    /// <summary>Returns the single family conversation where the user is the child.</summary>
    Task<Conversation?> GetFamilyByChildAsync(Guid childId, CancellationToken ct = default);

    /// <summary>Returns the support conversation for a specific client.</summary>
    Task<Conversation?> GetSupportByClientAsync(Guid clientId, CancellationToken ct = default);
}
