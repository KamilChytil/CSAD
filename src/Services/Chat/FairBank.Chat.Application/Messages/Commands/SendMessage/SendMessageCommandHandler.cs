using FairBank.Chat.Application.Messages.DTOs;
using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Messages.Commands.SendMessage;

public sealed record SendMessageCommand(
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Content,
    bool IsSystem = false) : IRequest<ChatMessageResponse>;

public sealed class SendMessageCommandHandler(IChatRepository msgRepo, IConversationRepository convRepo) : IRequestHandler<SendMessageCommand, ChatMessageResponse>
{
    public async Task<ChatMessageResponse> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var conversation = await convRepo.GetByIdAsync(request.ConversationId, ct)
                           ?? throw new InvalidOperationException("Conversation not found.");

        if (conversation.Status == Domain.Enums.ConversationStatus.Closed)
            throw new InvalidOperationException("Cannot send messages to a closed conversation.");

        var message = Domain.Aggregates.ChatMessage.Create(
            request.ConversationId,
            request.SenderId,
            request.SenderName,
            request.Content,
            request.IsSystem);

        // Record activity to know if it's Banker or Client typing
        // If it's a Support chat, client sends if SenderId == ClientOrChildId
        bool isClient = request.SenderId == conversation.ClientOrChildId;
        conversation.RecordMessageActivity(isClient);

        // Auto-claim logic: If a Banker sends a message to an unassigned Support conversation, assign it to them.
        if (!isClient && conversation.Type == Domain.Enums.ConversationType.Support && conversation.BankerOrParentId == null)
        {
            conversation.AssignBanker(request.SenderId);
        }

        await msgRepo.SaveMessageAsync(message, ct);
        await convRepo.UpdateAsync(conversation, ct);

        return new ChatMessageResponse(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.SenderName,
            message.Content,
            message.SentAt,
            message.IsSystem,
            message.ReadAt);
    }
}
