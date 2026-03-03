using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetPendingTransactions;

public sealed class GetPendingTransactionsQueryHandler(IPendingTransactionStore store)
    : IRequestHandler<GetPendingTransactionsQuery, IReadOnlyList<PendingTransactionResponse>>
{
    public async Task<IReadOnlyList<PendingTransactionResponse>> Handle(GetPendingTransactionsQuery request, CancellationToken ct)
    {
        var txs = await store.GetByAccountIdAsync(request.AccountId, ct);
        return txs.Select(t => new PendingTransactionResponse(
            t.Id, t.AccountId, t.Amount.Amount, t.Amount.Currency,
            t.Description, t.RequestedBy, t.Status, t.CreatedAt, t.ResolvedAt)).ToList();
    }
}
