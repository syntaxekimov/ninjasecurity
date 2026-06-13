using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using System.Security.Cryptography;

namespace AppName.Service.Engine;

public class ScanEngine : IScanEngine
{
    private readonly HashChecker _hashChecker;
    private readonly IClamAvScanner _clamAv;
    private readonly IYaraScanner _yara;
    private readonly ThreatScorer _scorer;

    public ScanEngine(
        HashChecker hashChecker,
        IClamAvScanner clamAv,
        IYaraScanner yara,
        ThreatScorer scorer)
    {
        _hashChecker = hashChecker;
        _clamAv = clamAv;
        _yara = yara;
        _scorer = scorer;
    }

    public async Task<ScanResult> ScanFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return ScanResult.Clean(filePath);

        // Layer 1: Hash check (fastest)
        var sha256 = await ComputeSha256Async(filePath, ct);
        var hashResult = await _hashChecker.IsKnownThreatAsync(sha256, ct);
        if (hashResult.IsKnown)
        {
            return new ScanResult(filePath, true, hashResult.ThreatName, 80, DetectionSource.Hash);
        }

        // Layer 2: ClamAV
        var clamResult = await _clamAv.ScanAsync(filePath, ct);
        if (!clamResult.ScanFailed && clamResult.IsInfected)
        {
            var score = _scorer.Score([new ScanResult(filePath, true, clamResult.VirusName, 70, DetectionSource.ClamAv)]);
            return new ScanResult(filePath, true, clamResult.VirusName, score, DetectionSource.ClamAv);
        }

        // Layer 3: YARA
        var yaraResult = await _yara.ScanAsync(filePath, ct);
        if (yaraResult.Matched)
        {
            return new ScanResult(filePath, true, yaraResult.RuleName, 50, DetectionSource.Yara);
        }

        return ScanResult.Clean(filePath);
    }

    public async Task<IReadOnlyList<ScanResult>> ScanDirectoryAsync(
        string dirPath, bool recursive = true, CancellationToken ct = default)
    {
        if (!Directory.Exists(dirPath))
            return [];

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(dirPath, "*.*", searchOption);
        var results = new List<ScanResult>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ScanFileAsync(file, ct);
            if (result.IsThreat)
                results.Add(result);
        }

        return results;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
