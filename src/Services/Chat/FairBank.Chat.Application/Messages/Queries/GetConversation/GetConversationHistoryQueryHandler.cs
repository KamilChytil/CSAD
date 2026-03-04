using FairBank.Chat.Application.Messages.DTOs;
using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Messages.Queries.GetConversation;

public sealed record GetConversationHistoryQuery(Guid ConversationId) : IRequest<IEnumerable<ChatMessageResponse>>;

public sealed class GetConversationHistoryQueryHandler(IChatRepository repo)
    : IRequestHandler<GetConversationHistoryQuery, IEnumerable<ChatMessageResponse>>
{
    public async Task<IEnumerable<ChatMessageResponse>> Handle(GetConversationHistoryQuery request, CancellationToken ct)
    {
        var messages = await repo.GetMessagesByConversationAsync(request.ConversationId, ct);
        return messages.Select(m => new ChatMessageResponse(m.Id, m.ConversationId, m.SenderId, m.SenderName, m.Content, m.SentAt, m.IsSystem, m.ReadAt));
    }
}
