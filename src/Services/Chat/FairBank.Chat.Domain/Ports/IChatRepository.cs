using FairBank.Chat.Domain.Aggregates;

namespace FairBank.Chat.Domain.Ports;

public interface IChatRepository
{
    Task SaveMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<IEnumerable<ChatMessage>> GetMessagesByConversationAsync(Guid conversationId, CancellationToken ct = default);
}
