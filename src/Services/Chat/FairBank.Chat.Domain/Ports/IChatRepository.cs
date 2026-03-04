using FairBank.Chat.Domain.Aggregates;

namespace FairBank.Chat.Domain.Ports;

public interface IChatRepository
{
    Task SaveMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken ct = default);
    Task UpdateMessageAsync(ChatMessage message, CancellationToken ct = default);
    Task<IEnumerable<ChatMessage>> GetMessagesByConversationAsync(Guid conversationId, CancellationToken ct = default);
    Task<(IReadOnlyList<ChatMessage> Messages, int TotalCount)> SearchMessagesAsync(
        Guid conversationId, string query, int page, int pageSize, CancellationToken ct = default);
}
