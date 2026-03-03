namespace FairBank.Chat.Application.Messages.DTOs;

public sealed record ChatMessageResponse(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Content,
    DateTime SentAt
);
