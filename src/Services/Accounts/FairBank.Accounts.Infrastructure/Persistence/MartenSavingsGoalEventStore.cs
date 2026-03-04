using FairBank.Accounts.Application.Ports;
using FairBank.Accounts.Domain.Aggregates;
using Marten;

namespace FairBank.Accounts.Infrastructure.Persistence;

public sealed class MartenSavingsGoalEventStore(IDocumentSession session) : ISavingsGoalEventStore
{
    public async Task<SavingsGoal?> LoadAsync(Guid goalId, CancellationToken ct = default)
    {
        return await session.Events.AggregateStreamAsync<SavingsGoal>(goalId, token: ct);
    }

    public async Task<IReadOnlyList<SavingsGoal>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default)
    {
        var results = await session.Query<SavingsGoal>()
            .Where(g => g.AccountId == accountId)
            .ToListAsync(ct);
        return results;
    }

    public async Task StartStreamAsync(SavingsGoal goal, CancellationToken ct = default)
    {
        var events = goal.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.StartStream<SavingsGoal>(goal.Id, events.ToArray());
        goal.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }

    public async Task AppendEventsAsync(SavingsGoal goal, CancellationToken ct = default)
    {
        var events = goal.GetUncommittedEvents();
        if (events.Count == 0) return;

        session.Events.Append(goal.Id, events.ToArray());
        goal.ClearUncommittedEvents();
        await session.SaveChangesAsync(ct);
    }
}
