using FairBank.Chat.Domain.Enums;

namespace FairBank.Chat.Domain.Aggregates;

public sealed class ChatMessage
{
    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string SenderName { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    private ChatMessage() { }

    public void MarkAsRead()
    {
        if (ReadAt is null)
            ReadAt = DateTime.UtcNow;
    }

    public static ChatMessage Create(Guid conversationId, Guid senderId, string senderName, string content)
        => new()
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            SenderId = senderId,
            SenderName = senderName,
            Content = content,
            SentAt = DateTime.UtcNow
        };
}
