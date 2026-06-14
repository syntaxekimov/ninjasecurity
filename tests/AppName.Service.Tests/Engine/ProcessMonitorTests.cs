using NinjaSecurity.Service.Engine;
using NinjaSecurity.Service.Engine.Interfaces;
using Moq;

namespace NinjaSecurity.Service.Tests.Engine;

public class ProcessMonitorTests
{
    [Theory]
    [InlineData(@"C:\Users\test\AppData\Local\Temp\evil.exe", false, 25)]
    [InlineData(@"C:\Users\test\Downloads\installer.exe", false, 15)]
    [InlineData(@"C:\Users\test\AppData\Roaming\payload.exe", false, 25)]
    public void ComputeRiskScore_SuspiciousPath_AddsRisk(string path, bool hasSig, int minExpected)
    {
        var score = ProcessMonitor.ComputeRiskScore(path, hasSig);
        Assert.True(score >= minExpected, $"Expected score >= {minExpected} for {path}, got {score}");
    }

    [Fact]
    public void ComputeRiskScore_UnsignedBinary_AddsRisk()
    {
        var signedScore = ProcessMonitor.ComputeRiskScore(@"C:\Windows\notepad.exe", hasSig: true);
        var unsignedScore = ProcessMonitor.ComputeRiskScore(@"C:\Windows\notepad.exe", hasSig: false);
        Assert.True(unsignedScore > signedScore);
    }

    [Theory]
    [InlineData("powershell.exe", true)]
    [InlineData("wscript.exe", true)]
    [InlineData("certutil.exe", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("explorer.exe", false)]
    public void IsLolBin_DetectsKnownBinaries(string name, bool expected)
    {
        Assert.Equal(expected, ProcessMonitor.IsLolBin(name));
    }

    [Fact]
    public void Start_SetsIsRunning_True()
    {
        var scanMock = new Mock<IScanEngine>();
        var monitor = new ProcessMonitor(scanMock.Object);
        monitor.Start();
        Assert.True(monitor.IsRunning);
        monitor.Stop();
    }

    [Fact]
    public void Stop_SetsIsRunning_False()
    {
        var scanMock = new Mock<IScanEngine>();
        var monitor = new ProcessMonitor(scanMock.Object);
        monitor.Start();
        monitor.Stop();
        Assert.False(monitor.IsRunning);
    }
}
