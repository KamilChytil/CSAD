using MediatR;

namespace FairBank.Accounts.Application.Commands.FreezeCard;

public sealed record FreezeCardCommand(Guid CardId) : IRequest;
