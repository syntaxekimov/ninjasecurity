using NinjaSecurity.Service.Engine;
using NinjaSecurity.Service.Engine.Interfaces;
using Moq;

namespace NinjaSecurity.Service.Tests.Engine;

public class SystemOptimizerTests
{
    [Fact]
    public async Task GetAutostartEntries_OnNonWindows_ReturnsEmpty()
    {
        if (OperatingSystem.IsWindows()) return; // skip on Windows

        var scanMock = new Mock<IScanEngine>();
        var optimizer = new SystemOptimizer(scanMock.Object);
        var entries = await optimizer.GetAutostartEntriesAsync();
        Assert.Empty(entries);
    }

    [Theory]
    [InlineData(false, "Registry\\Run", 30)] // unsigned = +30 minimum
    [InlineData(true, "Registry\\Run", 0)]   // signed, normal path = 0
    public void ComputeEntryRisk_ReflectsSignatureAndLocation(bool isSigned, string location, int minExpected)
    {
        var score = SystemOptimizer.ComputeEntryRisk(@"C:\Windows\legit.exe", isSigned, location);
        Assert.True(score >= minExpected);
    }

    [Fact]
    public void ComputeEntryRisk_SuspiciousPath_AddsScore()
    {
        var score = SystemOptimizer.ComputeEntryRisk(
            Path.Combine(Path.GetTempPath(), "evil.exe"),
            isSigned: false,
            location: "Registry\\Run");
        Assert.True(score >= 30); // unsigned minimum
    }

    [Fact]
    public void ComputeEntryRisk_LolBinInPath_AddsHighScore()
    {
        var score = SystemOptimizer.ComputeEntryRisk(
            @"C:\Windows\System32\powershell.exe",
            isSigned: false,
            location: "Registry\\Run");
        Assert.True(score >= 35); // LOLBin = +35
    }

    [Fact]
    public async Task GetTempFileSize_ReturnsNonNegative()
    {
        var scanMock = new Mock<IScanEngine>();
        var optimizer = new SystemOptimizer(scanMock.Object);
        var size = await optimizer.GetTempFileSizeAsync();
        Assert.True(size >= 0);
    }
}
