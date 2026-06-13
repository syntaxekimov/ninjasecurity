using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Moq;

namespace AppName.Service.Tests.Engine;

public class RealTimeGuardTests
{
    [Fact]
    public void Start_SetsIsRunning_True()
    {
        var scanMock = new Mock<IScanEngine>();
        var quarantineMock = new Mock<IQuarantineManager>();
        var guard = new RealTimeGuard(scanMock.Object, quarantineMock.Object, [Path.GetTempPath()]);

        guard.Start();
        Assert.True(guard.IsRunning);
        guard.Stop();
    }

    [Fact]
    public void Stop_AfterStart_SetsIsRunning_False()
    {
        var scanMock = new Mock<IScanEngine>();
        var quarantineMock = new Mock<IQuarantineManager>();
        var guard = new RealTimeGuard(scanMock.Object, quarantineMock.Object, [Path.GetTempPath()]);

        guard.Start();
        guard.Stop();
        Assert.False(guard.IsRunning);
    }

    [Fact]
    public void Start_Twice_DoesNotThrow()
    {
        var scanMock = new Mock<IScanEngine>();
        var quarantineMock = new Mock<IQuarantineManager>();
        var guard = new RealTimeGuard(scanMock.Object, quarantineMock.Object, [Path.GetTempPath()]);

        guard.Start();
        guard.Start();
        Assert.True(guard.IsRunning);
        guard.Stop();
    }

    [Theory]
    [InlineData("evil.exe", true)]
    [InlineData("script.ps1", true)]
    [InlineData("malware.dll", true)]
    [InlineData("document.txt", false)]
    [InlineData("image.png", false)]
    [InlineData("data.csv", false)]
    public void IsMonitoredExtension_CorrectlyClassifies(string fileName, bool expected)
    {
        Assert.Equal(expected, RealTimeGuard.IsMonitoredExtension(fileName));
    }
}
