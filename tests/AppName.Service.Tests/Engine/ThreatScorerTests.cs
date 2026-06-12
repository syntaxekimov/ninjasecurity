using AppName.Service.Engine;
using AppName.Service.Engine.Models;

namespace AppName.Service.Tests.Engine;

public class ThreatScorerTests
{
    private readonly ThreatScorer _scorer = new();

    [Fact]
    public void Score_NoDetections_ReturnsZero()
    {
        var result = _scorer.Score([]);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Score_HashDetection_Returns80()
    {
        var detections = new[]
        {
            new ScanResult("file.exe", true, "Hash.Known", 0, DetectionSource.Hash)
        };
        Assert.Equal(80, _scorer.Score(detections));
    }

    [Fact]
    public void Score_ClamAvDetection_Returns70()
    {
        var detections = new[]
        {
            new ScanResult("file.exe", true, "Trojan.Win32.X", 0, DetectionSource.ClamAv)
        };
        Assert.Equal(70, _scorer.Score(detections));
    }

    [Fact]
    public void Score_YaraDetection_Returns50()
    {
        var detections = new[]
        {
            new ScanResult("file.exe", true, "Yara.Suspicious", 0, DetectionSource.Yara)
        };
        Assert.Equal(50, _scorer.Score(detections));
    }

    [Fact]
    public void Score_MultipleDetections_CapsAt100()
    {
        var detections = new[]
        {
            new ScanResult("file.exe", true, "Hash.Known", 0, DetectionSource.Hash),
            new ScanResult("file.exe", true, "Trojan.Win32.X", 0, DetectionSource.ClamAv),
            new ScanResult("file.exe", true, "Yara.Sus", 0, DetectionSource.Yara)
        };
        Assert.Equal(100, _scorer.Score(detections));
    }

    [Theory]
    [InlineData(0, ThreatDisposition.Allow)]
    [InlineData(39, ThreatDisposition.Allow)]
    [InlineData(40, ThreatDisposition.Monitor)]
    [InlineData(69, ThreatDisposition.Monitor)]
    [InlineData(70, ThreatDisposition.Block)]
    [InlineData(100, ThreatDisposition.Block)]
    public void Disposition_CorrectThresholds(int score, ThreatDisposition expected)
    {
        Assert.Equal(expected, _scorer.Disposition(score));
    }
}
