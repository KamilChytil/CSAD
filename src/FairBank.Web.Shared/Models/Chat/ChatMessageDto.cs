namespace FairBank.Web.Shared.Models.Chat;

public sealed record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Content,
    DateTime SentAt,
    bool IsSystem = false,
    DateTime? ReadAt = null
);
