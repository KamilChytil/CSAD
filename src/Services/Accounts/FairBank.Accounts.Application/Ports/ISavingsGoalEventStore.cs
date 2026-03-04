using FairBank.Accounts.Domain.Aggregates;

namespace FairBank.Accounts.Application.Ports;

public interface ISavingsGoalEventStore
{
    Task<SavingsGoal?> LoadAsync(Guid goalId, CancellationToken ct = default);
    Task<IReadOnlyList<SavingsGoal>> LoadByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task StartStreamAsync(SavingsGoal goal, CancellationToken ct = default);
    Task AppendEventsAsync(SavingsGoal goal, CancellationToken ct = default);
}
