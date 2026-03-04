using MediatR;

namespace FairBank.Accounts.Application.Commands.DeactivateCard;

public sealed record DeactivateCardCommand(Guid CardId) : IRequest;
