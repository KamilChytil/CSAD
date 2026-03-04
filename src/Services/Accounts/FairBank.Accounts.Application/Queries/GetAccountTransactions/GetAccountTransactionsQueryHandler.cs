using FairBank.Accounts.Application.DTOs;
using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Events;
using MediatR;

namespace FairBank.Accounts.Application.Queries.GetAccountTransactions;

public sealed class GetAccountTransactionsQueryHandler : IRequestHandler<GetAccountTransactionsQuery, IReadOnlyList<TransactionDto>>
{
    private readonly IAccountEventStore _eventStore;

    public GetAccountTransactionsQueryHandler(IAccountEventStore eventStore)
    {
        _eventStore = eventStore;
    }

    public async Task<IReadOnlyList<TransactionDto>> Handle(GetAccountTransactionsQuery request, CancellationToken ct)
    {
        var raw = await _eventStore.GetStreamEventsAsync(request.AccountId, ct);

        var list = new List<TransactionDto>();
        foreach (var obj in raw)
        {
            switch (obj)
            {
                case Domain.Events.MoneyDeposited md:
                    if (InRange(md.OccurredAt))
                        list.Add(new TransactionDto(md.OccurredAt, "Deposit", md.Amount, md.Currency.ToString(), md.Description));
                    break;
                case Domain.Events.MoneyWithdrawn mw:
                    if (InRange(mw.OccurredAt))
                        list.Add(new TransactionDto(mw.OccurredAt, "Withdrawal", mw.Amount, mw.Currency.ToString(), mw.Description));
                    break;
            }
        }

        return list.OrderBy(t => t.OccurredAt).ToList();

        bool InRange(DateTime dt)
        {
            if (request.From.HasValue && dt < request.From.Value) return false;
            if (request.To.HasValue && dt > request.To.Value) return false;
            return true;
        }
    }
}
