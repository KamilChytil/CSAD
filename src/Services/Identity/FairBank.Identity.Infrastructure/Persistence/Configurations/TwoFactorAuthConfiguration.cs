using FairBank.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class TwoFactorAuthConfiguration : IEntityTypeConfiguration<TwoFactorAuth>
{
    public void Configure(EntityTypeBuilder<TwoFactorAuth> builder)
    {
        builder.ToTable("two_factor_auth");
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.UserId).IsUnique();
        builder.Property(t => t.SecretKey).HasMaxLength(200).IsRequired();
        builder.Property(t => t.BackupCodes).HasMaxLength(4000);
    }
}
