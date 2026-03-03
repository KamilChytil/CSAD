using Microsoft.EntityFrameworkCore;

namespace FairBank.Admin.Web.Data;

public class LogEntry
{
    public int Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "Information";
    public string Service { get; set; } = "Unknown";
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
}

public class LogDbContext : DbContext
{
    public LogDbContext(DbContextOptions<LogDbContext> options) : base(options) { }

    public DbSet<LogEntry> Logs => Set<LogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LogEntry>().HasIndex(l => l.Timestamp);
        modelBuilder.Entity<LogEntry>().HasIndex(l => l.Level);
        modelBuilder.Entity<LogEntry>().HasIndex(l => l.Service);
    }
}
