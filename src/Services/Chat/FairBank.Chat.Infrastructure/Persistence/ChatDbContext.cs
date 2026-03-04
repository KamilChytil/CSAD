using FairBank.Chat.Domain.Aggregates;
using FairBank.Chat.Domain.Entities;
using FairBank.Chat.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace FairBank.Chat.Infrastructure.Persistence;

public sealed class ChatDbContext(DbContextOptions<ChatDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<ChatAttachment> Attachments => Set<ChatAttachment>();

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
        conv.Property(c => c.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        conv.Property(c => c.ClosedAt);
        conv.Property(c => c.InternalNotes).HasMaxLength(4000);
        conv.Property(c => c.LastClientMessageAt);
        conv.Property(c => c.LastBankerMessageAt);

        // ── ChatMessage ───────────────────────────────────────────────────
        var msg = modelBuilder.Entity<ChatMessage>();
        msg.ToTable("messages");
        msg.HasKey(m => m.Id);
        msg.Property(m => m.ConversationId).IsRequired();
        msg.Property(m => m.SenderId).IsRequired();
        msg.Property(m => m.SenderName).IsRequired().HasMaxLength(200);
        msg.Property(m => m.Content).IsRequired().HasMaxLength(2000);
        msg.Property(m => m.SentAt).IsRequired();
        msg.Property(m => m.ReadAt);
        msg.Property(m => m.IsSystem).IsRequired().HasDefaultValue(false);
        msg.HasOne<Conversation>()
           .WithMany()
           .HasForeignKey(m => m.ConversationId)
           .OnDelete(DeleteBehavior.Cascade);

        // ── ChatAttachment ────────────────────────────────────────────────
        var att = modelBuilder.Entity<ChatAttachment>();
        att.ToTable("attachments");
        att.HasKey(a => a.Id);
        att.Property(a => a.MessageId).IsRequired();
        att.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        att.Property(a => a.ContentType).IsRequired().HasMaxLength(100);
        att.Property(a => a.FileSize).IsRequired();
        att.Property(a => a.StoragePath).IsRequired().HasMaxLength(1000);
        att.Property(a => a.CreatedAt).IsRequired();
        att.HasOne<ChatMessage>()
           .WithMany()
           .HasForeignKey(a => a.MessageId)
           .OnDelete(DeleteBehavior.Cascade);
    }
}
