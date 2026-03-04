using FairBank.Products.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairBank.Products.Infrastructure.Persistence.Configurations;

public sealed class ProductApplicationConfiguration : IEntityTypeConfiguration<ProductApplication>
{
    public void Configure(EntityTypeBuilder<ProductApplication> builder)
    {
        builder.ToTable("product_applications");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.ProductType).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Parameters).HasColumnType("text").IsRequired();
        builder.Property(p => p.MonthlyPayment).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.CreatedAt).IsRequired();
        builder.Property(p => p.Note).HasMaxLength(500);

        builder.HasIndex(p => p.UserId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.CreatedAt).IsDescending();
    }
}
