using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Conversations.Commands.AssignBanker;

public sealed record AssignConversationCommand(Guid ConversationId, Guid BankerId) : IRequest;

public sealed class AssignConversationCommandHandler(IConversationRepository repo) : IRequestHandler<AssignConversationCommand>
{
    public async Task Handle(AssignConversationCommand request, CancellationToken ct)
    {
        var conversation = await repo.GetByIdAsync(request.ConversationId, ct)
                           ?? throw new InvalidOperationException("Conversation not found.");

        conversation.AssignBanker(request.BankerId);
        await repo.UpdateAsync(conversation, ct);
    }
}
