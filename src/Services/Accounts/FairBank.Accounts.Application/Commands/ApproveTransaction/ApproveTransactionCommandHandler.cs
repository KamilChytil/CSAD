using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.ApproveTransaction;

public sealed class ApproveTransactionCommandHandler(
    IPendingTransactionStore pendingStore,
    IAccountEventStore accountStore,
    INotificationClient notificationClient)
    : IRequestHandler<ApproveTransactionCommand, PendingTransactionResponse>
{
    public async Task<PendingTransactionResponse> Handle(ApproveTransactionCommand request, CancellationToken ct)
    {
        var tx = await pendingStore.LoadAsync(request.TransactionId, ct)
            ?? throw new InvalidOperationException("Pending transaction not found.");

        tx.Approve(request.ApproverId);
        await pendingStore.AppendEventsAsync(tx, ct);

        // Execute the actual withdrawal
        var account = await accountStore.LoadAsync(tx.AccountId, ct)
            ?? throw new InvalidOperationException("Account not found.");

        account.Withdraw(tx.Amount, tx.Description);
        await accountStore.AppendEventsAsync(account, ct);

        // Notify child about approval
        await notificationClient.SendAsync(
            tx.RequestedBy,
            "TransactionApproved",
            "Platba schválena",
            $"Tvá platba {tx.Amount.Amount} {tx.Amount.Currency} byla schválena.",
            tx.Id, "PendingTransaction", ct);

        return new PendingTransactionResponse(
            tx.Id, tx.AccountId, tx.Amount.Amount, tx.Amount.Currency,
            tx.Description, tx.RequestedBy, tx.Status, tx.CreatedAt, tx.ResolvedAt);
    }
}
