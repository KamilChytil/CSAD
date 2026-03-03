using FairBank.Chat.Application.Messages.DTOs;
using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Conversations.Queries;

/// <summary>Returns conversations for a parent — one per child.</summary>
public sealed record GetParentConversationsQuery(Guid ParentId) : IRequest<IEnumerable<ConversationSummaryDto>>;

public sealed class GetParentConversationsQueryHandler(
    IConversationRepository convRepo,
    IChatRepository msgRepo)
    : IRequestHandler<GetParentConversationsQuery, IEnumerable<ConversationSummaryDto>>
{
    public async Task<IEnumerable<ConversationSummaryDto>> Handle(GetParentConversationsQuery request, CancellationToken ct)
    {
        var convs = await convRepo.GetFamilyByParentAsync(request.ParentId, ct);
        var results = new List<ConversationSummaryDto>();

        foreach (var conv in convs)
        {
            var last = (await msgRepo.GetMessagesByConversationAsync(conv.Id, ct)).LastOrDefault();
            results.Add(new ConversationSummaryDto(
                conv.Id, "Family", conv.Label,
                last?.Content, last?.SentAt,
                conv.Status.ToString(), conv.ClosedAt,
                conv.BankerOrParentId, conv.InternalNotes));
        }

        return results;
    }
}
