using AppName.Service.Engine.Interfaces;
using Moq;

namespace AppName.Service.Tests.Engine;

public class YaraScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenRulesMatch_ReturnsMatchedResult()
    {
        var mock = new Mock<IYaraScanner>();
        mock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YaraResult(true, "Suspicious_PE_Header"));

        var result = await mock.Object.ScanAsync("suspicious.exe");

        Assert.True(result.Matched);
        Assert.Equal("Suspicious_PE_Header", result.RuleName);
    }

    [Fact]
    public async Task ScanAsync_WhenNoRulesDirectory_ReturnsClean()
    {
        var scanner = new AppName.Service.Engine.YaraScanner("/nonexistent/path");

        var result = await scanner.ScanAsync("anyfile.exe");

        Assert.False(result.Matched);
        Assert.Null(result.RuleName);
    }
}
