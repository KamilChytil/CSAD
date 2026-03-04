namespace FairBank.Accounts.Domain.Events;

public sealed record CardSettingsChanged(
    Guid CardId, bool OnlinePaymentsEnabled, bool ContactlessEnabled, DateTime OccurredAt);
