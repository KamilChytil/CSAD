using FairBank.Chat.Application.Messages.DTOs;
using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Messages.Queries.GetConversation;

public sealed record GetConversationHistoryQuery(Guid User1Id, Guid User2Id) : IRequest<IEnumerable<ChatMessageResponse>>;

public sealed class GetConversationHistoryQueryHandler(IChatRepository chatRepository)
    : IRequestHandler<GetConversationHistoryQuery, IEnumerable<ChatMessageResponse>>
{
    public async Task<IEnumerable<ChatMessageResponse>> Handle(GetConversationHistoryQuery request, CancellationToken ct)
    {
        var messages = await chatRepository.GetMessagesBetweenUsersAsync(request.User1Id, request.User2Id, ct);
        
        return messages.Select(m => new ChatMessageResponse(
            m.Id,
            m.SenderId,
            m.ReceiverId,
            m.Content,
            m.SentAt));
    }
}
