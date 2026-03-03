using FairBank.Chat.Domain.Aggregates;
using FairBank.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Chat.Infrastructure.Persistence;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chat_service");
        
        var messageBuilder = modelBuilder.Entity<ChatMessage>();
        messageBuilder.ToTable("messages");
        messageBuilder.HasKey(m => m.Id);
        messageBuilder.Property(m => m.Content).IsRequired().HasMaxLength(2000);
        messageBuilder.Property(m => m.SenderId).IsRequired();
        messageBuilder.Property(m => m.ReceiverId).IsRequired();
        messageBuilder.Property(m => m.SentAt).IsRequired();
    }
}
