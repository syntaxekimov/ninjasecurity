using AppName.Service.Engine.Interfaces;
using nClam;

namespace AppName.Service.Engine;

public class ClamAvScanner : IClamAvScanner
{
    private readonly string _host;
    private readonly int _port;

    public ClamAvScanner(string host = "localhost", int port = 3310)
    {
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
        catch (Exception)
        {
            return new ClamAvResult(false, null);
        }
    }
}
