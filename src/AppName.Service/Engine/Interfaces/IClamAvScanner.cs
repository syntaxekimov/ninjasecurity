namespace AppName.Service.Engine.Interfaces;

public interface IClamAvScanner
{
    Task<ClamAvResult> ScanAsync(string filePath, CancellationToken ct = default);
}

public record ClamAvResult(bool IsInfected, string? VirusName, bool ScanFailed = false);
