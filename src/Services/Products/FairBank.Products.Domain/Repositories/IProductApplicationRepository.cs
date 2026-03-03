using FairBank.Products.Domain.Entities;
using FairBank.SharedKernel.Domain;

namespace FairBank.Products.Domain.Repositories;

public interface IProductApplicationRepository : IRepository<ProductApplication, Guid>
{
    Task<IReadOnlyList<ProductApplication>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ProductApplication>> GetPendingAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProductApplication>> GetAllAsync(int limit = 100, CancellationToken ct = default);
}
