using FairBank.SharedKernel.Domain;

namespace FairBank.Chat.Domain.Aggregates;

public sealed class ChatMessage : AggregateRoot<Guid>
{
    private ChatMessage() { }

    private ChatMessage(Guid senderId, Guid receiverId, string content)
    {
        Id = Guid.NewGuid();
        SenderId = senderId;
        ReceiverId = receiverId;
        Content = content;
        SentAt = DateTime.UtcNow;
    }

    public Guid SenderId { get; private set; }
    public Guid ReceiverId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }

    public static ChatMessage Create(Guid senderId, Guid receiverId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content cannot be empty.");

        return new ChatMessage(senderId, receiverId, content);
    }
}
