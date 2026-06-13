using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Moq;

namespace AppName.Service.Tests.Engine;

public class ScanEngineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IClamAvScanner> _clamMock;
    private readonly Mock<IYaraScanner> _yaraMock;
    private readonly HashChecker _hashChecker;
    private readonly ThreatScorer _scorer;
    private readonly ScanEngine _engine;

    public ScanEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _clamMock = new Mock<IClamAvScanner>();
        _yaraMock = new Mock<IYaraScanner>();
        _hashChecker = new HashChecker(Path.Combine(_tempDir, "hashes.db"));
        _scorer = new ThreatScorer();

        _clamMock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvResult(false, null));
        _yaraMock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YaraResult(false, null));

        _engine = new ScanEngine(_hashChecker, _clamMock.Object, _yaraMock.Object, _scorer);
    }

    [Fact]
    public async Task ScanFile_CleanFile_ReturnsCleanResult()
    {
        var file = Path.Combine(_tempDir, "clean.exe");
        await File.WriteAllTextAsync(file, "clean content");

        var result = await _engine.ScanFileAsync(file);

        Assert.False(result.IsThreat);
        Assert.Equal(0, result.ConfidenceScore);
    }

    [Fact]
    public async Task ScanFile_KnownHash_ReturnsThreat()
    {
        var file = Path.Combine(_tempDir, "evil.exe");
        await File.WriteAllTextAsync(file, "evil content");
        var sha256 = ComputeSha256(file);
        await _hashChecker.AddForTestingAsync(sha256, "Test.Trojan.KnownHash");

        var result = await _engine.ScanFileAsync(file);

        Assert.True(result.IsThreat);
        Assert.Equal(DetectionSource.Hash, result.Source);
        Assert.Equal(80, result.ConfidenceScore);
    }

    [Fact]
    public async Task ScanFile_ClamAvDetects_ReturnsThreat()
    {
        var file = Path.Combine(_tempDir, "infected.exe");
        await File.WriteAllTextAsync(file, "content");
        _clamMock.Setup(s => s.ScanAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvResult(true, "Trojan.Win32.X"));

        var result = await _engine.ScanFileAsync(file);

        Assert.True(result.IsThreat);
        Assert.Equal(DetectionSource.ClamAv, result.Source);
    }

    [Fact]
    public async Task ScanFile_NonExistentFile_ReturnsClean()
    {
        var result = await _engine.ScanFileAsync("/nonexistent/file.exe");
        Assert.False(result.IsThreat);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = System.Security.Cryptography.SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
