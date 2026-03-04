using MediatR;

namespace FairBank.Chat.Application.Messages.Queries.SearchMessages;

public sealed record SearchMessagesQuery(
    Guid ConversationId,
    string Query,
    int Page = 1,
    int PageSize = 20) : IRequest<SearchMessagesResult>;

public sealed record SearchMessagesResult(
    IReadOnlyList<ChatMessageDto> Messages,
    int TotalCount);

public sealed record ChatMessageDto(
    Guid Id,
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Content,
    DateTime SentAt,
    DateTime? ReadAt);
