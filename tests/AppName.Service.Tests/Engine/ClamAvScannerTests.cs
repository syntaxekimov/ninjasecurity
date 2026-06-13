using AppName.Service.Engine.Interfaces;
using Moq;

namespace AppName.Service.Tests.Engine;

public class ClamAvScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenMockReturnsInfected_ReturnsInfectedResult()
    {
        var mock = new Mock<IClamAvScanner>();
        mock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvResult(true, "Win.Test.EICAR_HDB-1"));

        var result = await mock.Object.ScanAsync("test.exe");

        Assert.True(result.IsInfected);
        Assert.Equal("Win.Test.EICAR_HDB-1", result.VirusName);
    }

    [Fact]
    public async Task ScanAsync_WhenMockReturnsClean_ReturnsCleanResult()
    {
        var mock = new Mock<IClamAvScanner>();
        mock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvResult(false, null));

        var result = await mock.Object.ScanAsync("clean.exe");

        Assert.False(result.IsInfected);
        Assert.Null(result.VirusName);
    }
}
