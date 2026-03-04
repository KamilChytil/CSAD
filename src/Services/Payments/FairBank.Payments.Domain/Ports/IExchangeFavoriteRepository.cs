using FairBank.Payments.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Payments.Domain.Ports;

public interface IExchangeFavoriteRepository : IRepository<ExchangeFavorite, Guid>
{
    Task<IReadOnlyList<ExchangeFavorite>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    void Remove(ExchangeFavorite entity);
}
