using FairBank.Cards.Domain.Aggregates;
using FairBank.Cards.Domain.Enums;
using FairBank.Cards.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Cards.Infrastructure.Persistence.Configurations;

public sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("cards");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.AccountId).IsRequired();
        builder.Property(c => c.UserId).IsRequired();

        builder.OwnsOne(c => c.CardNumber, cn =>
        {
            cn.Property(x => x.FullNumber)
                .HasColumnName("card_number_full")
                .HasMaxLength(16)
                .IsRequired();

            cn.Property(x => x.MaskedNumber)
                .HasColumnName("card_number_masked")
                .HasMaxLength(25)
                .IsRequired();

            cn.Property(x => x.LastFourDigits)
                .HasColumnName("card_number_last_four")
                .HasMaxLength(4)
                .IsRequired();
        });

        builder.Property(c => c.CardholderName)
            .HasColumnName("cardholder_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.ExpirationDate)
            .HasColumnName("expiration_date")
            .IsRequired();

        builder.Property(c => c.CardType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.CardBrand)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(c => c.DailyLimit)
            .HasColumnName("daily_limit")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(c => c.MonthlyLimit)
            .HasColumnName("monthly_limit")
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(c => c.OnlinePaymentsEnabled)
            .HasColumnName("online_payments_enabled")
            .IsRequired();

        builder.Property(c => c.ContactlessEnabled)
            .HasColumnName("contactless_enabled")
            .IsRequired();

        builder.Property(c => c.PinHash)
            .HasColumnName("pin_hash")
            .HasMaxLength(200);

        builder.Property(c => c.CreatedAt).IsRequired();
        builder.Property(c => c.UpdatedAt);

        builder.HasIndex(c => c.AccountId);
        builder.HasIndex(c => c.UserId);
        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.CreatedAt).IsDescending();
    }
}
