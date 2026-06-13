using AppName.Service.Engine.Interfaces;
using Microsoft.Extensions.Logging;
using nClam;

namespace AppName.Service.Engine;

public class ClamAvScanner : IClamAvScanner
{
    private readonly string _host;
    private readonly int _port;
    private readonly ILogger<ClamAvScanner> _logger;

    public ClamAvScanner(ILogger<ClamAvScanner> logger, string host = "localhost", int port = 3310)
    {
        _logger = logger;
        _host = host;
        _port = port;
    }

    public async Task<ClamAvResult> ScanAsync(string filePath, CancellationToken ct = default)
    {
        var client = new ClamClient(_host, _port);

        try
        {
            var result = await client.SendAndScanFileAsync(filePath);
            var isInfected = result.Result == ClamScanResults.VirusDetected;
            var virusName = result.InfectedFiles?.FirstOrDefault()?.VirusName;
            return new ClamAvResult(isInfected, virusName);
        }
        catch (Exception ex)
        {
            // ScanFailed=true lets ScanEngine skip this source rather than counting it as clean
            _logger.LogWarning(ex, "ClamAV scan failed for {FilePath} — clamd unavailable?", filePath);
            return new ClamAvResult(false, null, ScanFailed: true);
        }
    }
}
