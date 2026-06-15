using NinjaSecurity.Service.Engine.Interfaces;
using NinjaSecurity.Service.Engine.Models;
using NinjaSecurity.Service.Ipc;
using System.Text.Json;

namespace NinjaSecurity.Service.Engine;

public class ScanScheduler
{
    private readonly IScanEngine _scanEngine;
    private readonly IpcEventChannel _eventChannel;
    private readonly ILogger<ScanScheduler>? _logger;
    private readonly string _configPath;
    private ScanScheduleConfig _config;

    public ScanScheduleConfig Config => _config;

    public ScanScheduler(
        IScanEngine scanEngine,
        IpcEventChannel eventChannel,
        ILogger<ScanScheduler>? logger = null)
    {
        _scanEngine = scanEngine;
        _eventChannel = eventChannel;
        _logger = logger;
        _configPath = Path.Combine(AppPaths.AppData, "schedule.json");
        _config = LoadConfig();
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (_config.Enabled)
            {
                var lastRun = _config.LastRunUtc
                    ?? DateTime.UtcNow.AddHours(-_config.IntervalHours - 1);
                var nextRun = lastRun.AddHours(_config.IntervalHours);
                var delay = nextRun - DateTime.UtcNow;

                if (delay > TimeSpan.Zero)
                {
                    try { await Task.Delay(delay, ct); }
                    catch (OperationCanceledException) { break; }
                }

                await RunScanAsync(ct);
            }
            else
            {
                try { await Task.Delay(TimeSpan.FromMinutes(5), ct); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public void UpdateConfig(ScanScheduleConfig config)
    {
        _config = config;
        SaveConfig();
    }

    private async Task RunScanAsync(CancellationToken ct)
    {
        var path = _config.ScanType == "Full"
            ? Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\"
            : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        _logger?.LogInformation("Scheduled {Type} scan starting: {Path}", _config.ScanType, path);
        try
        {
            var results = await _scanEngine.ScanDirectoryAsync(path, recursive: true, ct);
            _config = _config with { LastRunUtc = DateTime.UtcNow };
            SaveConfig();

            foreach (var r in results)
                _eventChannel.Publish(new IpcEvent("ThreatFound",
                    JsonSerializer.SerializeToElement(
                        new ThreatFoundPayload(r.FilePath, r.ThreatName, r.ConfidenceScore))));

            _logger?.LogInformation("Scheduled scan done. Threats: {Count}", results.Count);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Scheduled scan failed");
        }
    }

    private ScanScheduleConfig LoadConfig()
    {
        if (!File.Exists(_configPath)) return new ScanScheduleConfig();
        try
        {
            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<ScanScheduleConfig>(json) ?? new ScanScheduleConfig();
        }
        catch { return new ScanScheduleConfig(); }
    }

    private void SaveConfig()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                _config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save schedule config");
        }
    }
}
