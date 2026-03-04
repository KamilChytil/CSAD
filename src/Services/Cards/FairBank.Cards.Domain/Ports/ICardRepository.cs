using FairBank.Cards.Domain.Aggregates;
using FairBank.SharedKernel.Domain;

namespace FairBank.Cards.Domain.Ports;

public interface ICardRepository : IRepository<Card, Guid>
{
    Task<IReadOnlyList<Card>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<Card>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
}
