using FairBank.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserDeviceConfiguration : IEntityTypeConfiguration<UserDevice>
{
    public void Configure(EntityTypeBuilder<UserDevice> builder)
    {
        builder.ToTable("user_devices");
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.UserId);
        builder.HasIndex(d => new { d.UserId, d.Browser, d.OperatingSystem, d.DeviceType })
            .HasDatabaseName("ix_user_devices_fingerprint");
        builder.Property(d => d.DeviceName).HasMaxLength(200).IsRequired();
        builder.Property(d => d.DeviceType).HasMaxLength(50);
        builder.Property(d => d.Browser).HasMaxLength(100);
        builder.Property(d => d.OperatingSystem).HasMaxLength(100);
        builder.Property(d => d.IpAddress).HasMaxLength(45); // IPv6
    }
}
