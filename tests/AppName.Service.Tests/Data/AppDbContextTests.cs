using NinjaSecurity.Service.Data;
using NinjaSecurity.Service.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace NinjaSecurity.Service.Tests.Data;

public class AppDbContextTests
{
    private static AppDbContext CreateInMemory()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task CanAddAndRetrieveQuarantineEntry()
    {
        await using var db = CreateInMemory();
        var entry = new QuarantineEntry
        {
            Id = Guid.NewGuid(),
            OriginalPath = @"C:\Users\test\Downloads\evil.exe",
            ThreatName = "Trojan.Win32.Test",
            ConfidenceScore = 90,
            Sha256 = "abc123",
            QuarantinedAt = DateTime.UtcNow,
            EncryptedFileName = "test.qvault",
            ProtectedKey = [1, 2, 3],
            Iv = [4, 5, 6]
        };

        db.QuarantineEntries.Add(entry);
        await db.SaveChangesAsync();

        var loaded = await db.QuarantineEntries.FindAsync(entry.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Trojan.Win32.Test", loaded.ThreatName);
        Assert.Equal(90, loaded.ConfidenceScore);
    }

    [Fact]
    public async Task CanAddAndRetrieveScanHistory()
    {
        await using var db = CreateInMemory();
        var entry = new ScanHistoryEntry
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ScanType = "Quick",
            FilesScanned = 1200,
            ThreatsFound = 0
        };

        db.ScanHistory.Add(entry);
        await db.SaveChangesAsync();

        var loaded = await db.ScanHistory.FindAsync(entry.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Quick", loaded.ScanType);
    }
}
