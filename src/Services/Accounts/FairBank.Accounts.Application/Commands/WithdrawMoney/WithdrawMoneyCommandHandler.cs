using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.WithdrawMoney;

public sealed class WithdrawMoneyCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<WithdrawMoneyCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(WithdrawMoneyCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        account.Withdraw(Money.Create(request.Amount, request.Currency), request.Description);

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
