using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetCardsByAccount;

public sealed record GetCardsByAccountQuery(Guid AccountId) : IRequest<IReadOnlyList<CardResponse>>;
