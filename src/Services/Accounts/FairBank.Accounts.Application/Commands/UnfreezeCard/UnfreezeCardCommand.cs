using MediatR;

namespace FairBank.Accounts.Application.Commands.UnfreezeCard;

public sealed record UnfreezeCardCommand(Guid CardId) : IRequest;
