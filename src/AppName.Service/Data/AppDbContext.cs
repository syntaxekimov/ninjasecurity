using AppName.Service.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppName.Service.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<QuarantineEntry> QuarantineEntries => Set<QuarantineEntry>();
    public DbSet<ScanHistoryEntry> ScanHistory => Set<ScanHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuarantineEntry>()
            .HasKey(e => e.Id);

        modelBuilder.Entity<ScanHistoryEntry>()
            .HasKey(e => e.Id);
    }
}
