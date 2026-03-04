using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountById;

public sealed class GetAccountByIdQueryHandler(IAccountEventStore eventStore)
    : IRequestHandler<GetAccountByIdQuery, AccountResponse?>
{
    public async Task<AccountResponse?> Handle(GetAccountByIdQuery request, CancellationToken ct)
    {
        var account = await eventStore.LoadAsync(request.AccountId, ct);

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
