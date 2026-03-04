using FairBank.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.UserEmail)
            .HasMaxLength(256);

        builder.Property(l => l.EntityName)
            .HasMaxLength(100);

        builder.Property(l => l.EntityId)
            .HasMaxLength(100);

        builder.Property(l => l.IpAddress)
            .HasMaxLength(50);

        builder.HasIndex(l => l.Timestamp);
        builder.HasIndex(l => l.UserId);
    }
}
