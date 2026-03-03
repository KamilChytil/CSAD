using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Conversations.Commands.ReopenConversation;

public sealed record ReopenConversationCommand(Guid ConversationId) : IRequest;

public sealed class ReopenConversationCommandHandler(IConversationRepository repo) : IRequestHandler<ReopenConversationCommand>
{
    public async Task Handle(ReopenConversationCommand request, CancellationToken ct)
    {
        var conversation = await repo.GetByIdAsync(request.ConversationId, ct)
                           ?? throw new InvalidOperationException("Conversation not found.");

        // Reopen throws if > 2 hours have passed since ClosedAt
        conversation.Reopen();
        await repo.UpdateAsync(conversation, ct);
    }
}
