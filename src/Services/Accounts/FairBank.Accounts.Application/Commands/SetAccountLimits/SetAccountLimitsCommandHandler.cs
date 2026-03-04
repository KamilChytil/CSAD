using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Commands.SetAccountLimits;

public sealed class SetAccountLimitsCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<SetAccountLimitsCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(SetAccountLimitsCommand request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct)
            ?? throw new InvalidOperationException("Account not found.");

        var limits = AccountLimits.Create(
            request.DailyTransactionLimit,
            request.MonthlyTransactionLimit,
            request.SingleTransactionLimit,
            request.DailyTransactionCount,
            request.OnlinePaymentLimit);

        account.SetAccountLimits(limits);
        await eventStore.AppendEventsAsync(account, ct);

        return new AccountResponse(
            account.Id, account.OwnerId, account.AccountNumber.Value,
            account.Balance.Amount, account.Balance.Currency,
            account.IsActive, account.CreatedAt, account.Alias);
    }
}
