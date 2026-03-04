using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreatePendingTransaction;

public sealed class CreatePendingTransactionCommandHandler(IPendingTransactionStore pendingStore)
    : IRequestHandler<CreatePendingTransactionCommand, PendingTransactionResponse>
{
    public async Task<PendingTransactionResponse> Handle(CreatePendingTransactionCommand request, CancellationToken ct)
    {
        var tx = PendingTransaction.Create(
            request.AccountId,
            Money.Create(request.Amount, request.Currency),
            request.Description,
            request.RequestedBy);

        await pendingStore.StartStreamAsync(tx, ct);

        return new PendingTransactionResponse(
            tx.Id, tx.AccountId, tx.Amount.Amount, tx.Amount.Currency,
            tx.Description, tx.RequestedBy, tx.Status, tx.CreatedAt, tx.ResolvedAt);
    }
}
