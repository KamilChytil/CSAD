using FairBank.Payments.Application.DTOs;
using FairBank.Payments.Domain.Ports;
using MediatR;

namespace FairBank.Payments.Application.StandingOrders.Queries.GetStandingOrdersByAccount;

public sealed record GetStandingOrdersByAccountQuery(Guid AccountId) : IRequest<IReadOnlyList<StandingOrderResponse>>;

public sealed class GetStandingOrdersByAccountQueryHandler(
    IStandingOrderRepository repository) : IRequestHandler<GetStandingOrdersByAccountQuery, IReadOnlyList<StandingOrderResponse>>
{
    public async Task<IReadOnlyList<StandingOrderResponse>> Handle(GetStandingOrdersByAccountQuery request, CancellationToken ct)
    {
        var orders = await repository.GetByAccountIdAsync(request.AccountId, ct);

        return orders.Select(so => new StandingOrderResponse(
            so.Id, so.SenderAccountId, so.SenderAccountNumber, so.RecipientAccountNumber,
            so.Amount, so.Currency.ToString(), so.Description,
            so.Interval.ToString(), so.NextExecutionDate, so.EndDate,
            so.IsActive, so.CreatedAt, so.LastExecutedAt, so.ExecutionCount)).ToList();
    }
}
