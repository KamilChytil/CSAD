namespace FairBank.Chat.Application.Messages.DTOs;

public sealed record ChatMessageResponse(
    Guid Id,
    Guid SenderId,
    Guid ReceiverId,
    string Content,
    DateTime SentAt);
