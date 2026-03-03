using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Conversations.Commands.UpdateNotes;

public sealed record UpdateConversationNotesCommand(Guid ConversationId, string Notes) : IRequest;

public sealed class UpdateConversationNotesCommandHandler(IConversationRepository repo) : IRequestHandler<UpdateConversationNotesCommand>
{
    public async Task Handle(UpdateConversationNotesCommand request, CancellationToken ct)
    {
        var conversation = await repo.GetByIdAsync(request.ConversationId, ct)
                           ?? throw new InvalidOperationException("Conversation not found.");

        conversation.UpdateNotes(request.Notes);
        await repo.UpdateAsync(conversation, ct);
    }
}
