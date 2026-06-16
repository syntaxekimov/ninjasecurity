using NinjaSecurity.Service.Data;
using NinjaSecurity.Service.Engine;
using NinjaSecurity.Service.Engine.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace NinjaSecurity.Service.Tests.Engine;

public class QuarantineManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _quarantineDir;
    private readonly AppDbContext _db;
    private readonly QuarantineManager _manager;

    public QuarantineManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _quarantineDir = Path.Combine(_tempDir, "Quarantine");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_quarantineDir);

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);
        _manager = new QuarantineManager(_db, new PassthroughDataProtector(), NullLogger<QuarantineManager>.Instance, _quarantineDir);
    }

    [Fact]
    public async Task Isolate_MovesFileToQuarantine()
    {
        var testFile = Path.Combine(_tempDir, "evil.exe");
        await File.WriteAllTextAsync(testFile, "malicious content");
        var threat = new ScanResult(testFile, true, "Trojan.Test", 90, DetectionSource.Hash);

        var id = await _manager.IsolateAsync(testFile, threat);

        Assert.False(File.Exists(testFile));
        var entries = await _manager.ListAsync();
        Assert.Single(entries);
        Assert.Equal("Trojan.Test", entries[0].ThreatName);
        Assert.Equal(id, entries[0].Id);
    }

    [Fact]
    public async Task Restore_ReturnsFileToOriginalPath()
    {
        var testFile = Path.Combine(_tempDir, "evil.exe");
        var originalContent = "malicious content";
        await File.WriteAllTextAsync(testFile, originalContent);
        var threat = new ScanResult(testFile, true, "Trojan.Test", 90, DetectionSource.Hash);

        var id = await _manager.IsolateAsync(testFile, threat);
        await _manager.RestoreAsync(id);

        Assert.True(File.Exists(testFile));
        Assert.Equal(originalContent, await File.ReadAllTextAsync(testFile));
        var entries = await _manager.ListAsync();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task Delete_RemovesFromDiskAndDb()
    {
        var testFile = Path.Combine(_tempDir, "evil.exe");
        await File.WriteAllTextAsync(testFile, "bad");
        var threat = new ScanResult(testFile, true, "Trojan.Test", 80, DetectionSource.ClamAv);

        var id = await _manager.IsolateAsync(testFile, threat);
        await _manager.DeleteAsync(id);

        var entries = await _manager.ListAsync();
        Assert.Empty(entries);
        Assert.Empty(Directory.GetFiles(_quarantineDir, "*.qvault"));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
