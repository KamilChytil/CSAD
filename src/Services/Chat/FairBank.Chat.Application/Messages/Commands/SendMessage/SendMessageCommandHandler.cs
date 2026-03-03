using FairBank.Chat.Application.Messages.DTOs;
using FairBank.Chat.Domain.Ports;
using MediatR;

namespace FairBank.Chat.Application.Messages.Commands.SendMessage;

public sealed record SendMessageCommand(
    Guid ConversationId,
    Guid SenderId,
    string SenderName,
    string Content) : IRequest<ChatMessageResponse>;

public sealed class SendMessageCommandHandler(IChatRepository repo) : IRequestHandler<SendMessageCommand, ChatMessageResponse>
{
    public async Task<ChatMessageResponse> Handle(SendMessageCommand request, CancellationToken ct)
    {
        var message = Domain.Aggregates.ChatMessage.Create(
            request.ConversationId,
            request.SenderId,
            request.SenderName,
            request.Content);

        await repo.SaveMessageAsync(message, ct);

        return new ChatMessageResponse(
            message.Id,
            message.ConversationId,
            message.SenderId,
            message.SenderName,
            message.Content,
            message.SentAt);
    }
}
