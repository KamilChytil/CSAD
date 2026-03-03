using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Ports;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence.Repositories;

public sealed class StandingOrderRepository(PaymentsDbContext context) : IStandingOrderRepository
{
    public async Task<StandingOrder?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.StandingOrders.FirstOrDefaultAsync(so => so.Id == id, ct);

    public async Task AddAsync(StandingOrder aggregate, CancellationToken ct = default)
        => await context.StandingOrders.AddAsync(aggregate, ct);

    public Task UpdateAsync(StandingOrder aggregate, CancellationToken ct = default)
    {
        context.StandingOrders.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<StandingOrder>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default)
        => await context.StandingOrders
            .Where(so => so.SenderAccountId == accountId)
            .OrderByDescending(so => so.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<StandingOrder>> GetDueOrdersAsync(DateTime currentDate, CancellationToken ct = default)
        => await context.StandingOrders
            .Where(so => so.IsActive && so.NextExecutionDate <= currentDate.Date)
            .ToListAsync(ct);
}
