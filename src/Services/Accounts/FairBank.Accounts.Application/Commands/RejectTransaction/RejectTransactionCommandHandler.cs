using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.RejectTransaction;

public sealed class RejectTransactionCommandHandler(
    IPendingTransactionStore pendingStore,
    INotificationClient notificationClient)
    : IRequestHandler<RejectTransactionCommand, PendingTransactionResponse>
{
    public async Task<PendingTransactionResponse> Handle(RejectTransactionCommand request, CancellationToken ct)
    {
        var tx = await pendingStore.LoadAsync(request.TransactionId, ct)
            ?? throw new InvalidOperationException("Pending transaction not found.");

        tx.Reject(request.ApproverId, request.Reason);
        await pendingStore.AppendEventsAsync(tx, ct);

        // Notify child about rejection
        await notificationClient.SendAsync(
            tx.RequestedBy,
            "TransactionRejected",
            "Platba zamítnuta",
            $"Tvá platba {tx.Amount.Amount} {tx.Amount.Currency} byla zamítnuta: {request.Reason}",
            tx.Id, "PendingTransaction", ct);

        return new PendingTransactionResponse(
            tx.Id, tx.AccountId, tx.Amount.Amount, tx.Amount.Currency,
            tx.Description, tx.RequestedBy, tx.Status, tx.CreatedAt, tx.ResolvedAt);
    }
}
