using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountByNumber;

public sealed record GetAccountByNumberQuery(string AccountNumber) : IRequest<AccountResponse?>;

public sealed class GetAccountByNumberQueryHandler(IAccountEventStore eventStore)
    : IRequestHandler<GetAccountByNumberQuery, AccountResponse?>
{
    public async Task<AccountResponse?> Handle(GetAccountByNumberQuery request, CancellationToken ct)
    {
        var account = await eventStore.LoadByAccountNumberAsync(request.AccountNumber, ct);

        if (account is null) return null;

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
            account.SpendingLimit?.Amount,
            account.AccountType);
    }
}
