using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using MediatR;

namespace FairBank.Accounts.Application.Commands.CreateAccount;

public sealed class CreateAccountCommandHandler(IAccountEventStore eventStore)
    : IRequestHandler<CreateAccountCommand, AccountResponse>
{
    public async Task<AccountResponse> Handle(CreateAccountCommand request, CancellationToken ct)
    {
        var account = Account.Create(request.OwnerId, request.Currency);

        await eventStore.StartStreamAsync(account, ct);

        return new AccountResponse(
            account.Id,
            account.OwnerId,
            account.AccountNumber.Value,
            account.Balance.Amount,
            account.Balance.Currency,
            account.IsActive,
            account.CreatedAt);
    }
}
