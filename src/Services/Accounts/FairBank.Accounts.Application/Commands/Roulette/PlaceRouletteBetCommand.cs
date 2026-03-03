using FairBank.Accounts.Domain.Enums;
using MediatR;

namespace FairBank.Accounts.Application.Commands.Roulette;

public sealed record PlaceRouletteBetCommand(
    Guid AccountId,
    decimal Amount,
    Currency Currency,
    RouletteBetType BetType,
    int? Number = null
) : IRequest<RouletteBetResultResponse>;