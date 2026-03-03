using FairBank.Accounts.Application.DTOs;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetPendingTransactions;

public sealed record GetPendingTransactionsQuery(Guid AccountId) : IRequest<IReadOnlyList<PendingTransactionResponse>>;
