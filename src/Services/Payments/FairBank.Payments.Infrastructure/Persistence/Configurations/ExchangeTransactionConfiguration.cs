using FairBank.Payments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Payments.Infrastructure.Persistence.Configurations;

public sealed class ExchangeTransactionConfiguration : IEntityTypeConfiguration<ExchangeTransaction>
{
    public void Configure(EntityTypeBuilder<ExchangeTransaction> builder)
    {
        builder.ToTable("exchange_transactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.SourceAccountId).HasColumnName("source_account_id").IsRequired();
        builder.Property(e => e.TargetAccountId).HasColumnName("target_account_id").IsRequired();
        builder.Property(e => e.FromCurrency).HasColumnName("from_currency").HasMaxLength(10).IsRequired();
        builder.Property(e => e.ToCurrency).HasColumnName("to_currency").HasMaxLength(10).IsRequired();
        builder.Property(e => e.SourceAmount).HasColumnName("source_amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.TargetAmount).HasColumnName("target_amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.ExchangeRate).HasColumnName("exchange_rate").HasColumnType("decimal(18,6)").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(e => e.UserId);
        builder.HasIndex(e => e.CreatedAt).IsDescending();
    }
}
