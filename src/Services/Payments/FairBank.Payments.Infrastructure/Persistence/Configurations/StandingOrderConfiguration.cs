using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Payments.Infrastructure.Persistence.Configurations;

public sealed class StandingOrderConfiguration : IEntityTypeConfiguration<StandingOrder>
{
    public void Configure(EntityTypeBuilder<StandingOrder> builder)
    {
        builder.ToTable("standing_orders");

        builder.HasKey(so => so.Id);
        builder.Property(so => so.Id).ValueGeneratedNever();

        builder.Property(so => so.SenderAccountId).IsRequired();

        builder.Property(so => so.SenderAccountNumber)
            .HasColumnName("sender_account_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(so => so.RecipientAccountNumber)
            .HasColumnName("recipient_account_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(so => so.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(so => so.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(so => so.Description).HasMaxLength(500);

        builder.Property(so => so.Interval)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(so => so.NextExecutionDate).IsRequired();
        builder.Property(so => so.EndDate);
        builder.Property(so => so.IsActive).IsRequired();
        builder.Property(so => so.CreatedAt).IsRequired();
        builder.Property(so => so.LastExecutedAt);
        builder.Property(so => so.ExecutionCount).IsRequired();

        builder.HasIndex(so => so.SenderAccountId);
        builder.HasIndex(so => new { so.IsActive, so.NextExecutionDate });
    }
}
