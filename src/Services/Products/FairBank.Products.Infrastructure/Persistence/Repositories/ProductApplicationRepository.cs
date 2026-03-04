using FairBank.Products.Domain.Entities;
using FairBank.Products.Domain.Enums;
using FairBank.Products.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Products.Infrastructure.Persistence.Repositories;

public sealed class ProductApplicationRepository(ProductsDbContext context) : IProductApplicationRepository
{
    public async Task<ProductApplication?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.ProductApplications.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AddAsync(ProductApplication aggregate, CancellationToken ct = default)
        => await context.ProductApplications.AddAsync(aggregate, ct);

    public Task UpdateAsync(ProductApplication aggregate, CancellationToken ct = default)
    {
        context.ProductApplications.Update(aggregate);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ProductApplication>> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.ProductApplications
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductApplication>> GetPendingAsync(CancellationToken ct = default)
        => await context.ProductApplications
            .Where(p => p.Status == ApplicationStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProductApplication>> GetAllAsync(int limit = 100, CancellationToken ct = default)
        => await context.ProductApplications
            .OrderByDescending(p => p.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
