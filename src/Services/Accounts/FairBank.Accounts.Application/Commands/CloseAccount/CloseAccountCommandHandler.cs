using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CloseAccount;

public sealed class CloseAccountCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<CloseAccountCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(CloseAccountCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account '{request.AccountId}' not found.");

        account.Close();
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
