using FairBank.Chat.Application.Hubs;
using FairBank.Chat.Domain.Ports;
using MediatR;
using Microsoft.AspNetCore.SignalR;

namespace FairBank.Chat.Application.Conversations.Commands.AssignBanker;

public sealed record AssignConversationCommand(Guid ConversationId, Guid BankerId, string? BankerName = null) : IRequest;

public sealed class AssignConversationCommandHandler(
    IConversationRepository repo, 
    IHubContext<ChatHub> hubContext) : IRequestHandler<AssignConversationCommand>
{
    public async Task Handle(AssignConversationCommand request, CancellationToken ct)
    {
        var conversation = await repo.GetByIdAsync(request.ConversationId, ct)
                           ?? throw new InvalidOperationException("Conversation not found.");

        bool isTransfer = conversation.BankerOrParentId != null;
        conversation.AssignBanker(request.BankerId);
        await repo.UpdateAsync(conversation, ct);

        // Broadcast system message
        string systemText = isTransfer 
            ? $"Chat byl předán bankéři {request.BankerName ?? "kolegovi"}."
            : $"Bankéř {request.BankerName ?? "podpory"} se připojil k chatu.";

        await hubContext.Clients.Group($"conv-{request.ConversationId}")
            .SendAsync("ReceiveMessage", new
            {
                Id = Guid.NewGuid(),
                ConversationId = request.ConversationId,
                SenderId = Guid.Empty,
                SenderName = "Systém",
                Content = systemText,
                SentAt = DateTime.UtcNow,
                IsSystem = true
            }, cancellationToken: ct);
    }
}
