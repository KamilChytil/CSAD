using FairBank.Chat.Domain.Enums;

namespace FairBank.Chat.Domain.Aggregates;

public sealed class Conversation
{
    public Guid Id { get; private set; }
    public ConversationType Type { get; private set; }

    /// <summary>The client (Support) or child (Family) side of the conversation.</summary>
    public Guid ClientOrChildId { get; private set; }

    /// <summary>null = any banker (Support); parentId for Family.</summary>
    public Guid? BankerOrParentId { get; private set; }

    /// <summary>Human-readable label for the conversation list.</summary>
    public string Label { get; private set; } = string.Empty;

    public DateTime CreatedAt { get; private set; }

    private Conversation() { }

    public static Conversation CreateSupport(Guid clientId, string clientLabel)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Support,
            ClientOrChildId = clientId,
            BankerOrParentId = null,
            Label = clientLabel,
            CreatedAt = DateTime.UtcNow
        };

    public static Conversation CreateFamily(Guid parentId, Guid childId, string childLabel)
        => new()
        {
            Id = Guid.NewGuid(),
            Type = ConversationType.Family,
            ClientOrChildId = childId,
            BankerOrParentId = parentId,
            Label = childLabel,
            CreatedAt = DateTime.UtcNow
        };
}
