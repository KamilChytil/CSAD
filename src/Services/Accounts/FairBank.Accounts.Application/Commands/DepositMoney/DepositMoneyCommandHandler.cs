using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.DepositMoney;

public sealed class DepositMoneyCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<DepositMoneyCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(DepositMoneyCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException($"Account {request.AccountId} not found.");

        account.Deposit(Money.Create(request.Amount, request.Currency), request.Description);

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
