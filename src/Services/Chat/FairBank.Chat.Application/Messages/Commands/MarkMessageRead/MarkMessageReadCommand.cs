using MediatR;

namespace FairBank.Chat.Application.Messages.Commands.MarkMessageRead;

public sealed record MarkMessageReadCommand(Guid MessageId) : IRequest;
