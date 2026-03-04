using FairBank.Payments.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Payments.Infrastructure.Persistence.Configurations;

public sealed class ExchangeFavoriteConfiguration : IEntityTypeConfiguration<ExchangeFavorite>
{
    public void Configure(EntityTypeBuilder<ExchangeFavorite> builder)
    {
        builder.ToTable("exchange_favorites");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.FromCurrency).HasColumnName("from_currency").HasMaxLength(10).IsRequired();
        builder.Property(e => e.ToCurrency).HasColumnName("to_currency").HasMaxLength(10).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(e => e.UserId);
    }
}
