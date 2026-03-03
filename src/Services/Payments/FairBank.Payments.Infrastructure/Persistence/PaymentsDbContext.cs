using FairBank.Payments.Domain.Entities;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Payments.Infrastructure.Persistence;

public sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<StandingOrder> StandingOrders => Set<StandingOrder>();
    public DbSet<PaymentTemplate> PaymentTemplates => Set<PaymentTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("payments_service");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
    }
}
