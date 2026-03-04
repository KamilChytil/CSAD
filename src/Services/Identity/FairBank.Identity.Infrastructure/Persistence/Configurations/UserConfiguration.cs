using FairBank.Identity.Domain.Entities;
using FairBank.Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Identity.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.Email)
            .HasConversion(e => e.Value, v => FairBank.Identity.Domain.ValueObjects.Email.Create(v))
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(u => u.IsActive).IsRequired();
        builder.Property(u => u.IsDeleted).IsRequired();
        builder.Property(u => u.CreatedAt).IsRequired();

        // Parent-child self-reference
        builder.Property(u => u.ParentId);

        builder.HasOne(u => u.Parent)
            .WithMany(u => u.Children)
            .HasForeignKey(u => u.ParentId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(u => u.ParentId);

        builder.Navigation(u => u.Children).HasField("_children");

        // ── Security Settings ──────────────────────────────
        builder.Property(u => u.AllowInternationalPayments)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.NightTransactionsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(u => u.RequireApprovalAbove)
            .HasPrecision(18, 2);

        // ── Two-Factor Authentication ────────────────────────
        builder.Property(u => u.IsTwoFactorEnabled)
            .IsRequired()
            .HasDefaultValue(false);

        // Security — login lockout & single-session
        builder.Property(u => u.FailedLoginAttempts)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(u => u.LockedUntil);

        builder.Property(u => u.ActiveSessionId);

        builder.Property(u => u.SessionExpiresAt);

        // ── KYC Data ──────────────────────────────────────────
        builder.Property(u => u.PersonalIdNumber).HasMaxLength(20);

        builder.Property(u => u.DateOfBirth);

        builder.Property(u => u.PhoneNumber)
            .HasConversion(
                p => p != null ? p.Value : null,
                p => p != null ? PhoneNumber.Create(p) : null)
            .HasColumnName("PhoneNumber")
            .HasMaxLength(20);

        builder.OwnsOne(u => u.Address, address =>
        {
            address.Property(a => a.Street).HasColumnName("Street").HasMaxLength(200);
            address.Property(a => a.City).HasColumnName("City").HasMaxLength(100);
            address.Property(a => a.ZipCode).HasColumnName("ZipCode").HasMaxLength(20);
            address.Property(a => a.Country).HasColumnName("Country").HasMaxLength(100);
        });

        builder.Property(u => u.AgreedToTermsAt);

        // ── Email Verification ──────────────────────────────
        builder.Property(u => u.IsEmailVerified)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(u => u.EmailVerificationToken).HasMaxLength(200);
        builder.Property(u => u.EmailVerificationTokenExpiresAt);

        // ── Password Reset ──────────────────────────────────
        builder.Property(u => u.PasswordResetToken).HasMaxLength(200);
        builder.Property(u => u.PasswordResetTokenExpiresAt);

        // Global query filter: soft delete
        builder.HasQueryFilter(u => !u.IsDeleted);
    }
}
