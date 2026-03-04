using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Messages.Queries.SearchMessages;

public sealed class SearchMessagesQueryHandler(IChatRepository repo)
    : IRequestHandler<SearchMessagesQuery, SearchMessagesResult>
{
    public async Task<SearchMessagesResult> Handle(SearchMessagesQuery request, CancellationToken ct)
    {
        var (messages, totalCount) = await repo.SearchMessagesAsync(
            request.ConversationId, request.Query, request.Page, request.PageSize, ct);

        var dtos = messages.Select(m => new ChatMessageDto(
            m.Id, m.ConversationId, m.SenderId, m.SenderName, m.Content, m.SentAt, m.ReadAt)).ToList();

        return new SearchMessagesResult(dtos, totalCount);
    }
}
