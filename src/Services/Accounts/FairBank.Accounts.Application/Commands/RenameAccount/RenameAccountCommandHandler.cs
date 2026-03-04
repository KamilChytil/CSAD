using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.RenameAccount;

public sealed class RenameAccountCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<RenameAccountCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(RenameAccountCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account '{request.AccountId}' not found.");

        account.Rename(request.Alias);
        await eventStore.AppendEventsAsync(account, ct);

        return new AccountResponse(
            account.Id,
            account.OwnerId,
            account.AccountNumber.Value,
            account.Balance.Amount,
            account.Balance.Currency,
            account.IsActive,
            account.CreatedAt,
            account.Alias,
            account.RequiresApproval,
            account.ApprovalThreshold?.Amount,
            account.SpendingLimit?.Amount);
    }
}
