using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Messages.Commands.MarkMessageRead;

public sealed class MarkMessageReadCommandHandler(IChatRepository repo) : IRequestHandler<MarkMessageReadCommand>
{
    public async Task Handle(MarkMessageReadCommand request, CancellationToken ct)
    {
        var message = await repo.GetByIdAsync(request.MessageId, ct)
                      ?? throw new InvalidOperationException("Message not found.");

        message.MarkAsRead();
        await repo.UpdateMessageAsync(message, ct);
    }
}
