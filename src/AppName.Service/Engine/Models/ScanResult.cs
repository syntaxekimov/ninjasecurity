namespace AppName.Service.Engine.Models;

public record ScanResult(
    string FilePath,
    bool IsThreat,
    string? ThreatName,
    int ConfidenceScore,
    DetectionSource Source
)
{
    public static ScanResult Clean(string filePath) =>
        new(filePath, false, null, 0, DetectionSource.None);
}

