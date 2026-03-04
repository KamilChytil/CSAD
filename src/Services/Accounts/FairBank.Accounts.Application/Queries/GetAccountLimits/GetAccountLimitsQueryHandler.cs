using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.ValueObjects;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountLimits;

public sealed class GetAccountLimitsQueryHandler(IAccountEventStore eventStore)
    : IRequestHandler<GetAccountLimitsQuery, AccountLimitsResponse?>
{
    public async Task<AccountLimitsResponse?> Handle(GetAccountLimitsQuery request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct);

        if (account is null) return null;

        var limits = account.Limits ?? AccountLimits.Default();

        return new AccountLimitsResponse(
            limits.DailyTransactionLimit,
            limits.MonthlyTransactionLimit,
            limits.SingleTransactionLimit,
            limits.DailyTransactionCount,
            limits.OnlinePaymentLimit,
            0, 0, 0); // Usage will be calculated by frontend or a future aggregation
    }
}
