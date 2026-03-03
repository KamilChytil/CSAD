using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountById;

public sealed record GetAccountByIdQuery(Guid AccountId) : IRequest<AccountResponse?>;
