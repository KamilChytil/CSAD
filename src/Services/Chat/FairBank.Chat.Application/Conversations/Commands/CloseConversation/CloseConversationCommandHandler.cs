using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Conversations.Commands.CloseConversation;

public sealed record CloseConversationCommand(Guid ConversationId) : IRequest;

public sealed class CloseConversationCommandHandler(IConversationRepository repo) : IRequestHandler<CloseConversationCommand>
{
    public async Task Handle(CloseConversationCommand request, CancellationToken ct)
    {
        var conversation = await repo.GetByIdAsync(request.ConversationId, ct)
                           ?? throw new InvalidOperationException("Conversation not found.");

        conversation.Close();
        await repo.UpdateAsync(conversation, ct);
    }
}
