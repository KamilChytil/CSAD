using FairBank.Chat.Domain.Enums;

namespace FairBank.Chat.Domain.Aggregates;

public sealed class Conversation
{
    public Guid Id { get; private set; }
    public ConversationType Type { get; private set; }

    /// <summary>The client (Support) or child (Family) side of the conversation.</summary>
    public Guid ClientOrChildId { get; private set; }

    /// <summary>null = any banker (Support); parentId for Family. Also serves as AssignedBankerId.</summary>
    public Guid? BankerOrParentId { get; private set; }

    /// <summary>Human-readable label for the conversation list.</summary>
    public string Label { get; private set; } = string.Empty;

    public ConversationStatus Status { get; private set; } = ConversationStatus.Active;
    public DateTime? ClosedAt { get; private set; }
    public string? InternalNotes { get; private set; } = string.Empty;

    public DateTime? LastClientMessageAt { get; private set; }
    public DateTime? LastBankerMessageAt { get; private set; }
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
            Status = ConversationStatus.Active,
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
            Status = ConversationStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

    public void AssignBanker(Guid bankerId)
    {
        if (Type != ConversationType.Support)
            throw new InvalidOperationException("Only support conversations can be assigned to bankers.");

        if (Status == ConversationStatus.Closed)
            throw new InvalidOperationException("Cannot assign a closed conversation.");

        BankerOrParentId = bankerId;
    }

    public void UnassignBanker()
    {
        if (Type != ConversationType.Support)
            throw new InvalidOperationException("Only support conversations can be unassigned.");

        BankerOrParentId = null;
    }

    public void UpdateNotes(string notes)
    {
        InternalNotes = notes ?? string.Empty;
    }

    public void Close()
    {
        if (Status == ConversationStatus.Closed)
            return;

        Status = ConversationStatus.Closed;
        ClosedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        if (Status != ConversationStatus.Closed)
            return;

        if (ClosedAt.HasValue && (DateTime.UtcNow - ClosedAt.Value).TotalHours > 2)
            throw new InvalidOperationException("Conversation cannot be reopened after 2 hours from closing.");

        Status = ConversationStatus.Active;
        ClosedAt = null;
    }

    public void RecordMessageActivity(bool isFromClient)
    {
        if (Status == ConversationStatus.Closed)
            throw new InvalidOperationException("Cannot send messages to a closed conversation.");

        var now = DateTime.UtcNow;
        if (isFromClient)
        {
            LastClientMessageAt = now;
        }
        else
        {
            LastBankerMessageAt = now;
        }
    }

    public bool IsUnassignedSupport(TimeSpan autoReleaseTimeout)
    {
        if (Type != ConversationType.Support) return false;
        
        // Not claimed at all
        if (!BankerOrParentId.HasValue) return true;

        // Claimed, but client asked something and banker ignored it for autoReleaseTimeout (2h)
        if (LastClientMessageAt.HasValue && 
            (!LastBankerMessageAt.HasValue || LastClientMessageAt > LastBankerMessageAt) &&
            (DateTime.UtcNow - LastClientMessageAt.Value) > autoReleaseTimeout)
        {
            return true;
        }

        return false;
    }
}
