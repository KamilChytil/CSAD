using FairBank.Chat.Application.Hubs;
using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Ports;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace FairBank.Chat.Application.Conversations.Commands.CloseConversation;

public sealed record CloseConversationCommand(Guid ConversationId) : IRequest;

public sealed class CloseConversationCommandHandler(
    IConversationRepository repo,
    IChatRepository chatRepo,
    IHubContext<ChatHub> hubContext) : IRequestHandler<CloseConversationCommand>
{
    public async Task Handle(CloseConversationCommand request, CancellationToken ct)
    {
        var conversation = await repo.GetByIdAsync(request.ConversationId, ct)
                           ?? throw new InvalidOperationException("Conversation not found.");

        conversation.Close();
        await repo.UpdateAsync(conversation, ct);

        // Persist system message
        var systemMsg = ChatMessage.Create(
            request.ConversationId,
            Guid.Empty,
            "Systém",
            "Chat byl uzavřen.",
            isSystem: true);

        await chatRepo.SaveMessageAsync(systemMsg, ct);

        // Broadcast system message
        await hubContext.Clients.Group($"conv-{request.ConversationId}")
            .SendAsync("ReceiveMessage", new
            {
                systemMsg.Id,
                systemMsg.ConversationId,
                systemMsg.SenderId,
                systemMsg.SenderName,
                systemMsg.Content,
                systemMsg.SentAt,
                systemMsg.IsSystem
            }, cancellationToken: ct);
    }
}
