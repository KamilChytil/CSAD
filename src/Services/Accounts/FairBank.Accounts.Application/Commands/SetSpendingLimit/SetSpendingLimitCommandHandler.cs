using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SetSpendingLimit;

public sealed class SetSpendingLimitCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<SetSpendingLimitCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(SetSpendingLimitCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException("Account not found.");

        account.SetSpendingLimit(Money.Create(request.Limit, request.Currency));
        await eventStore.AppendEventsAsync(account, ct);

        return new AccountResponse(
            account.Id, account.OwnerId, account.AccountNumber.Value,
            account.Balance.Amount, account.Balance.Currency,
            account.IsActive, account.CreatedAt, account.Alias,
            account.RequiresApproval,
            account.ApprovalThreshold?.Amount,
            account.SpendingLimit?.Amount);
    }
}
