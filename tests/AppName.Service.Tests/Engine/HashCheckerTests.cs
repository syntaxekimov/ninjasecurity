using AppName.Service.Engine;

namespace AppName.Service.Tests.Engine;

public class HashCheckerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly HashChecker _checker;

    public HashCheckerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        _checker = new HashChecker(_dbPath);
    }

    [Fact]
    public async Task Check_EmptyDatabase_ReturnsFalse()
    {
        var result = await _checker.IsKnownThreatAsync("abc123def456");
        Assert.False(result.IsKnown);
        Assert.Null(result.ThreatName);
    }

    [Fact]
    public async Task Check_KnownHash_ReturnsTrue()
    {
        await _checker.AddForTestingAsync("deadbeef01234567", "Test.Trojan.FakeHash");

        var result = await _checker.IsKnownThreatAsync("deadbeef01234567");

        Assert.True(result.IsKnown);
        Assert.Equal("Test.Trojan.FakeHash", result.ThreatName);
    }

    [Fact]
    public async Task Check_CaseInsensitive_ReturnsTrue()
    {
        await _checker.AddForTestingAsync("ABCDEF123456", "Test.Virus");

        var result = await _checker.IsKnownThreatAsync("abcdef123456");
        Assert.True(result.IsKnown);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
