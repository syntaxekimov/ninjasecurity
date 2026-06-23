using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using NinjaSecurity.Service.Engine.Interfaces;
using NinjaSecurity.Service.Engine.Models;

namespace NinjaSecurity.Service.Engine;

public class UpdateService : IUpdateService
{
    private readonly HttpClient? _http;
    private readonly IClamAvDaemon? _clamAvDaemon;
    private readonly ILogger<UpdateService>? _logger;

    public DateTime? LastUpdated { get; private set; }

    public UpdateService(
        HttpClient? httpClient = null,
        IClamAvDaemon? clamAvDaemon = null,
        ILogger<UpdateService>? logger = null)
    {
        _http          = httpClient;
        _clamAvDaemon  = clamAvDaemon;
        _logger        = logger;
    }

    public Task<UpdateInfo> CheckUpdatesAsync(CancellationToken ct = default) =>
        Task.FromResult(new UpdateInfo(
            SignaturesAvailable:  true,
            YaraRulesAvailable:   true,
            MalSearcherAvailable: true,
            LastChecked:          DateTime.UtcNow));

    public async Task<AppUpdateInfo> CheckAppUpdateAsync(CancellationToken ct = default)
    {
        if (_http is null)
            return new AppUpdateInfo(false, null, null, null);

        const string currentVersion = "0.1.0";
        const string releasesUrl =
            "https://api.github.com/repos/syntaxekimov/ninjasecurity/releases/latest";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, releasesUrl);
            req.Headers.UserAgent.ParseAdd("NinjaSecurity/1.0");
            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
                return new AppUpdateInfo(false, null, null, null);

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var tag    = doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v');
            var htmlUrl = doc.RootElement.GetProperty("html_url").GetString();
            var body   = doc.RootElement.TryGetProperty("body", out var b) ? b.GetString() : null;

            // Strip pre-release suffix before version comparison
            var updateAvailable = tag is not null
                && Version.TryParse(tag.Split('-')[0], out var latest)
                && Version.TryParse(currentVersion, out var current)
                && latest > current;

            return new AppUpdateInfo(updateAvailable, tag, htmlUrl, body);
        }
        catch
        {
            return new AppUpdateInfo(false, null, null, null);
        }
    }

    public async Task<bool> UpdateSignaturesAsync(CancellationToken ct = default)
    {
        if (_clamAvDaemon is null) return false;
        try
        {
            await _clamAvDaemon.UpdateDefinitionsAsync(ct);
            LastUpdated = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ClamAV signature update failed");
            return false;
        }
    }

    public async Task<bool> UpdateYaraRulesAsync(CancellationToken ct = default)
    {
        if (_http is null) return false;

        const string zipUrl =
            "https://github.com/Neo23x0/signature-base/archive/refs/heads/master.zip";
        var tempZip = Path.Combine(Path.GetTempPath(), "ninja-yara-rules.zip");
        var tempDir = Path.Combine(Path.GetTempPath(), "ninja-yara-extract");
        try
        {
            _logger?.LogInformation("Downloading YARA rules from signature-base...");
            using var req = new HttpRequestMessage(HttpMethod.Get, zipUrl);
            req.Headers.UserAgent.ParseAdd("NinjaSecurity/1.0");
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            await using (var fs = File.Create(tempZip))
                await resp.Content.CopyToAsync(fs, ct);

            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            ZipFile.ExtractToDirectory(tempZip, tempDir);

            var yaraSource = Directory.GetDirectories(tempDir)
                .Select(d => Path.Combine(d, "yara"))
                .FirstOrDefault(Directory.Exists);
            if (yaraSource is null)
            {
                _logger?.LogWarning("YARA folder not found inside downloaded archive");
                return false;
            }

            Directory.CreateDirectory(AppPaths.RulesPath);
            var count = 0;
            foreach (var file in Directory.EnumerateFiles(yaraSource, "*.yar"))
            {
                File.Copy(file, Path.Combine(AppPaths.RulesPath, Path.GetFileName(file)),
                    overwrite: true);
                count++;
            }

            LastUpdated = DateTime.UtcNow;
            _logger?.LogInformation("YARA rules updated: {Count} files installed to {Path}", count, AppPaths.RulesPath);
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "YARA rules update failed");
            return false;
        }
        finally
        {
            try { File.Delete(tempZip); } catch { }
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    public async Task<bool> UpdateMalSearcherDbAsync(CancellationToken ct = default)
    {
        if (_http is null) return false;
        try
        {
            _logger?.LogInformation("Fetching recent samples from MalwareBazaar...");
            var inserted = 0;
            await using var conn = new SqliteConnection($"Data Source={AppPaths.HashDbPath}");
            await conn.OpenAsync(ct);

            await ExecAsync(conn, """
                CREATE TABLE IF NOT EXISTS hashes (
                    sha256 TEXT PRIMARY KEY,
                    threat_name TEXT NOT NULL
                )
                """, ct);

            foreach (var selector in new[] { "time", "exe", "dll", "doc", "ps1", "jar", "zip" })
            {
                try
                {
                    var content = new StringContent(
                        $"query=get_recent&selector={selector}",
                        Encoding.UTF8,
                        "application/x-www-form-urlencoded");
                    using var req = new HttpRequestMessage(HttpMethod.Post, "https://bazaar.abuse.ch/api/")
                        { Content = content };
                    req.Headers.UserAgent.ParseAdd("NinjaSecurity/1.0");
                    using var resp = await _http.SendAsync(req, ct);
                    if (!resp.IsSuccessStatusCode) continue;

                    await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    if (doc.RootElement.GetProperty("query_status").GetString() != "ok") continue;

                    foreach (var sample in doc.RootElement.GetProperty("data").EnumerateArray())
                    {
                        var sha256 = sample.TryGetProperty("sha256_hash", out var h)
                            ? h.GetString()?.ToLowerInvariant() : null;
                        if (sha256 is null || sha256.Length != 64) continue;

                        var sig  = sample.TryGetProperty("signature", out var s) ? s.GetString() : null;
                        string?  tag = null;
                        if (sample.TryGetProperty("tags", out var tags) && tags.GetArrayLength() > 0)
                            tag = tags[0].GetString();
                        var name = sig ?? (tag is not null ? $"Malware.{tag}" : "Malware.Unknown");

                        using var cmd = conn.CreateCommand();
                        cmd.CommandText =
                            "INSERT OR IGNORE INTO hashes(sha256, threat_name) VALUES(@s, @n)";
                        cmd.Parameters.AddWithValue("@s", sha256);
                        cmd.Parameters.AddWithValue("@n", name);
                        inserted += await cmd.ExecuteNonQueryAsync(ct);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger?.LogWarning(ex, "MalwareBazaar fetch failed for selector {Selector}", selector);
                }
            }

            LastUpdated = DateTime.UtcNow;
            _logger?.LogInformation("Hash database updated: {Count} new entries", inserted);
            return inserted > 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Hash database update failed");
            return false;
        }
    }

    private static async Task ExecAsync(SqliteConnection conn, string sql, CancellationToken ct)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
