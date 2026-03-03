using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Chat.Infrastructure.Persistence;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("chat_service");

        // ── Conversation ──────────────────────────────────────────────────
        var conv = modelBuilder.Entity<Conversation>();
        conv.ToTable("conversations");
        conv.HasKey(c => c.Id);
        conv.Property(c => c.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        conv.Property(c => c.ClientOrChildId).IsRequired();
        conv.Property(c => c.BankerOrParentId);
        conv.Property(c => c.Label).IsRequired().HasMaxLength(200);
        conv.Property(c => c.CreatedAt).IsRequired();

        // ── ChatMessage ───────────────────────────────────────────────────
        var msg = modelBuilder.Entity<ChatMessage>();
        msg.ToTable("messages");
        msg.HasKey(m => m.Id);
        msg.Property(m => m.ConversationId).IsRequired();
        msg.Property(m => m.SenderId).IsRequired();
        msg.Property(m => m.SenderName).IsRequired().HasMaxLength(200);
        msg.Property(m => m.Content).IsRequired().HasMaxLength(2000);
        msg.Property(m => m.SentAt).IsRequired();
        msg.HasOne<Conversation>()
           .WithMany()
           .HasForeignKey(m => m.ConversationId)
           .OnDelete(DeleteBehavior.Cascade);
    }
}
