using FairBank.Chat.Application.Messages.DTOs;
using FairBank.Chat.Domain.Enums;
using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Conversations.Queries;

/// <summary>
/// Returns conversations visible to a user based on their role.
/// - Employee/Banker: all Support conversations
/// - Client:          their own Support conversation (created on first call) + family conversation if child
/// - Parent:          all Family conversations for their children
/// - Child:           their single Family conversation with their parent
/// </summary>
public sealed record GetConversationsQuery(
    Guid UserId,
    string UserRole,
    string UserLabel,
    Guid? ParentId      // set when caller is a child account
) : IRequest<IEnumerable<ConversationSummaryDto>>;

public sealed class GetConversationsQueryHandler(
    IConversationRepository convRepo,
    IChatRepository msgRepo)
    : IRequestHandler<GetConversationsQuery, IEnumerable<ConversationSummaryDto>>
{
    public async Task<IEnumerable<ConversationSummaryDto>> Handle(GetConversationsQuery request, CancellationToken ct)
    {
        var results = new List<ConversationSummaryDto>();
        var autoReleaseTimeout = TimeSpan.FromHours(2);

        if (request.UserRole is "Employee" or "Admin" or "Banker")
        {
            // Bankers/admins see ALL support conversations
            var all = await convRepo.GetAllSupportAsync(ct);
            foreach (var conv in all)
            {
                var messages = await msgRepo.GetMessagesByConversationAsync(conv.Id, ct);
                var last = messages.LastOrDefault();

                // 1. Filter out empty unassigned chats
                if (last == null && conv.BankerOrParentId == null)
                    continue;

                // 2. Lock logic: If no one responded for > 24 hours, automatically close (internally)
                var lastActivity = conv.LastClientMessageAt ?? conv.LastBankerMessageAt ?? conv.CreatedAt;
                if (conv.Status == ConversationStatus.Active && (DateTime.UtcNow - lastActivity).TotalHours > 24)
                {
                    conv.Close();
                    await convRepo.UpdateAsync(conv, ct);
                }

                Guid? effectiveBankerId = conv.BankerOrParentId;
                if (conv.IsUnassignedSupport(autoReleaseTimeout))
                {
                    effectiveBankerId = null; // Forces it into Unassigned bucket
                }

                results.Add(new ConversationSummaryDto(
                    conv.Id, "Support", conv.Label,
                    last?.Content, last?.SentAt,
                    conv.Status.ToString(), conv.ClosedAt,
                    effectiveBankerId, conv.InternalNotes));
            }
        }
        else
        {
            // Clients always have (or get) a Support conversation
            var support = await convRepo.GetOrCreateSupportAsync(request.UserId, request.UserLabel, ct);
            var lastSupport = (await msgRepo.GetMessagesByConversationAsync(support.Id, ct)).LastOrDefault();
            results.Add(new ConversationSummaryDto(
                support.Id, "Support", "Banker support",
                lastSupport?.Content, lastSupport?.SentAt,
                support.Status.ToString(), support.ClosedAt,
                support.BankerOrParentId, support.InternalNotes));

            // If the user is a child, also return their family conversation
            if (request.ParentId.HasValue)
            {
                var family = await convRepo.GetOrCreateFamilyAsync(
                    request.ParentId.Value, request.UserId, request.UserLabel, ct);
                var lastFamily = (await msgRepo.GetMessagesByConversationAsync(family.Id, ct)).LastOrDefault();
                results.Add(new ConversationSummaryDto(
                    family.Id, "Family", "Parent",
                    lastFamily?.Content, lastFamily?.SentAt,
                    family.Status.ToString(), family.ClosedAt,
                    family.BankerOrParentId, family.InternalNotes));
            }
        }

        return results;
    }
}
