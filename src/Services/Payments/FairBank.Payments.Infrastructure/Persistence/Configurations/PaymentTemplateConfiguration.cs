using FairBank.Payments.Domain.Entities;
using FairBank.Payments.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Payments.Infrastructure.Persistence.Configurations;

public sealed class PaymentTemplateConfiguration : IEntityTypeConfiguration<PaymentTemplate>
{
    public void Configure(EntityTypeBuilder<PaymentTemplate> builder)
    {
        builder.ToTable("payment_templates");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.OwnerAccountId).IsRequired();

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.RecipientAccountNumber)
            .HasColumnName("recipient_account_number")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.RecipientName).HasMaxLength(200);

        builder.Property(t => t.DefaultAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(t => t.Currency)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(t => t.DefaultDescription).HasMaxLength(500);
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.UpdatedAt);
        builder.Property(t => t.IsDeleted).IsRequired();

        builder.HasIndex(t => t.OwnerAccountId);
        builder.HasQueryFilter(t => !t.IsDeleted);
    }
}
