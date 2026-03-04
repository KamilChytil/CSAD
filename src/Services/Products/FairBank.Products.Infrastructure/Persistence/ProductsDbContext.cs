using FairBank.Products.Domain.Entities;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Products.Infrastructure.Persistence;

public sealed class ProductsDbContext(DbContextOptions<ProductsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<ProductApplication> ProductApplications => Set<ProductApplication>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("products_service");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductsDbContext).Assembly);
    }
}
