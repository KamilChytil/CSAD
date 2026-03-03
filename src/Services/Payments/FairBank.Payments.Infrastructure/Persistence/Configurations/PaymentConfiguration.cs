using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Payments.Infrastructure.Persistence.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.SenderAccountId).IsRequired();
        builder.Property(p => p.RecipientAccountId);

        builder.Property(p => p.SenderAccountNumber)
            .HasColumnName("sender_account_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.RecipientAccountNumber)
            .HasColumnName("recipient_account_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(p => p.Amount)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(p => p.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.Description).HasMaxLength(500);

        builder.Property(p => p.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.FailureReason).HasMaxLength(500);
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.CompletedAt);

        builder.Property(p => p.TemplateId);
        builder.Property(p => p.StandingOrderId);

        builder.HasIndex(p => p.SenderAccountId);
        builder.HasIndex(p => p.RecipientAccountId);
        builder.HasIndex(p => p.CreatedAt).IsDescending();
        builder.HasIndex(p => p.Status);
    }
}
