using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountsByOwner;

public sealed class GetAccountsByOwnerQueryHandler(IAccountEventStore eventStore)
    : IRequestHandler<GetAccountsByOwnerQuery, IReadOnlyList<AccountResponse>>
{
    public async Task<IReadOnlyList<AccountResponse>> Handle(GetAccountsByOwnerQuery request, CancellationToken ct)
    {
        var accounts = await eventStore.LoadByOwnerAsync(request.OwnerId, ct);

        return accounts
            .Select(a => new AccountResponse(
                a.Id,
                a.OwnerId,
                a.AccountNumber.Value,
                a.Balance.Amount,
                a.Balance.Currency,
                a.IsActive,
                a.CreatedAt,
                a.Alias,
                a.RequiresApproval,
                a.ApprovalThreshold?.Amount,
                a.SpendingLimit?.Amount))
            .ToList();
    }
}
