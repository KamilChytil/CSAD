using MediatR;

namespace FairBank.Cards.Application.Commands.DeactivateAllCardsByUser;

public sealed record DeactivateAllCardsByUserCommand(Guid UserId) : IRequest<int>;
