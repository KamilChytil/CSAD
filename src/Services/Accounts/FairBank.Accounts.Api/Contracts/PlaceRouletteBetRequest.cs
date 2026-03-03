using FairBank.Accounts.Domain.Enums;

namespace FairBank.Accounts.Api.Contracts;

public sealed record PlaceRouletteBetRequest(
    decimal Amount,
    Currency Currency,
    RouletteBetType BetType,
    int? Number
);