using NinjaSecurity.Service.Engine.Interfaces;
using NinjaSecurity.Service.Engine.Models;
using Microsoft.Extensions.Logging;

namespace NinjaSecurity.Service.Engine;

public class UpdateService : IUpdateService
{
    private readonly HttpClient? _http;
    private readonly string _yaraRulesPath;
    private readonly string _databasesPath;
    private readonly ILogger<UpdateService>? _logger;

    public DateTime? LastUpdated { get; private set; }

    public UpdateService(
        HttpClient? httpClient = null,
        string? yaraRulesPath = null,
        string? databasesPath = null,
        ILogger<UpdateService>? logger = null)
    {
        _http = httpClient;
        _yaraRulesPath = yaraRulesPath ?? AppPaths.YaraRulesPath;
        _databasesPath = databasesPath ?? AppPaths.DatabasesPath;
        _logger = logger;
    }

    public Task<UpdateInfo> CheckUpdatesAsync(CancellationToken ct = default) =>
        Task.FromResult(new UpdateInfo(
            SignaturesAvailable: true,
            YaraRulesAvailable: true,
            MalSearcherAvailable: true,
            LastChecked: DateTime.UtcNow));

    public async Task<bool> UpdateSignaturesAsync(CancellationToken ct = default)
    {
        if (_http is null) return false;
        try
        {
            _logger?.LogInformation("Updating ClamAV signatures...");
            var destPath = Path.Combine(_databasesPath, "daily.cvd");
            var downloaded = await DownloadAsync(
                new Uri("https://database.clamav.net/daily.cvd"), destPath, ct);
            if (downloaded) LastUpdated = DateTime.UtcNow;
            return downloaded;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ClamAV signature update failed");
            return false;
        }
    }

    public Task<bool> UpdateYaraRulesAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_yaraRulesPath))
        {
            _logger?.LogWarning("YARA rules path does not exist: {Path}", _yaraRulesPath);
            return Task.FromResult(false);
        }
        // TODO: download and extract YARA rules tarball
        return Task.FromResult(false);
    }

    public Task<bool> UpdateMalSearcherDbAsync(CancellationToken ct = default)
    {
        if (_http is null) return Task.FromResult(false);
        // TODO: download latest release from GitHub
        return Task.FromResult(false);
    }

    private async Task<bool> DownloadAsync(Uri uri, string destPath, CancellationToken ct)
    {
        try
        {
            var response = await _http!.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var tempPath = destPath + ".tmp";
            await using (var fs = File.Create(tempPath))
                await response.Content.CopyToAsync(fs, ct);

            File.Move(tempPath, destPath, overwrite: true);
            _logger?.LogInformation("Downloaded {Uri} → {Dest}", uri, destPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Download failed: {Uri}", uri);
            return false;
        }
    }
}
