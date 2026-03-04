using FairBank.Cards.Domain.Aggregates;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Cards.Infrastructure.Persistence;

public sealed class CardsDbContext(DbContextOptions<CardsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Card> Cards => Set<Card>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("cards_service");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CardsDbContext).Assembly);
    }
}
