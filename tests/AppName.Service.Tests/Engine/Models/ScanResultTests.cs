using AppName.Service.Engine.Models;

namespace AppName.Service.Tests.Engine.Models;

public class ScanResultTests
{
    [Fact]
    public void Clean_ReturnsScanResultWithNoThreat()
    {
        var result = ScanResult.Clean("/some/file.exe");

        Assert.Equal("/some/file.exe", result.FilePath);
        Assert.False(result.IsThreat);
        Assert.Null(result.ThreatName);
        Assert.Equal(0, result.ConfidenceScore);
        Assert.Equal(DetectionSource.None, result.Source);
    }

    [Fact]
    public void ThreatDisposition_HasAllThreeValues()
    {
        var values = Enum.GetValues<ThreatDisposition>();
        Assert.Contains(ThreatDisposition.Allow, values);
        Assert.Contains(ThreatDisposition.Monitor, values);
        Assert.Contains(ThreatDisposition.Block, values);
    }
}
