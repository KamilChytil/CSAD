using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Ports;
using FairBank.SharedKernel.Application;
using MediatR;

namespace FairBank.Chat.Application.Messages.Commands.SendMessage;

public sealed record SendMessageCommand(Guid SenderId, Guid ReceiverId, string Content) : IRequest<Guid>;

public sealed class SendMessageCommandHandler(IChatRepository chatRepository, IUnitOfWork unitOfWork)
    : IRequestHandler<SendMessageCommand, Guid>
{
    public async Task<Guid> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var message = ChatMessage.Create(request.SenderId, request.ReceiverId, request.Content);
        
        await chatRepository.AddAsync(message, ct);
        await unitOfWork.SaveChangesAsync(ct);
        
        return message.Id;
    }
}
