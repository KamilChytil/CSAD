namespace FairBank.Web.Shared.Models.Chat;

public sealed record ChatMessageDto(
    Guid Id,
    Guid SenderId,
    Guid ReceiverId,
    string Content,
    DateTime SentAt);
