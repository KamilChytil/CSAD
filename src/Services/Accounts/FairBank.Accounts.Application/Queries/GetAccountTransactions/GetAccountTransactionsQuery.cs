using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountTransactions;

public sealed record GetAccountTransactionsQuery(
    Guid AccountId,
    DateTime? From,
    DateTime? To) : IRequest<IReadOnlyList<TransactionDto>>;
