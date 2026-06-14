using NinjaSecurity.Service.Engine.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace NinjaSecurity.Service.Engine;

public class RealTimeGuard : IRealTimeGuard
{
    private static readonly string[] MonitoredExtensions =
        [".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr"];

    private static readonly string[] DefaultWatchPaths =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Path.GetTempPath(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
    ];

    private readonly IScanEngine _scanEngine;
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IQuarantineManager? _quarantineDirect;
    private readonly ILogger<RealTimeGuard>? _logger;
    private readonly string[] _watchPaths;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly RansomwareDetector _ransomwareDetector;
    private bool _running;

    public bool IsRunning => _running;
    public event EventHandler<string>? ThreatDetected;

    [ActivatorUtilitiesConstructor]
    public RealTimeGuard(
        IScanEngine scanEngine,
        IServiceScopeFactory scopeFactory,
        string[]? watchPaths = null,
        ILogger<RealTimeGuard>? logger = null,
        RansomwareDetector? ransomwareDetector = null)
    {
        _scanEngine = scanEngine;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _watchPaths = watchPaths is { Length: > 0 } ? watchPaths : DefaultWatchPaths;
        _ransomwareDetector = ransomwareDetector ?? new RansomwareDetector();
        _ransomwareDetector.AlarmRaised += OnRansomwareAlarm;
    }

    public RealTimeGuard(
        IScanEngine scanEngine,
        IQuarantineManager quarantine,
        string[]? watchPaths = null,
        ILogger<RealTimeGuard>? logger = null,
        RansomwareDetector? ransomwareDetector = null)
    {
        _scanEngine = scanEngine;
        _quarantineDirect = quarantine;
        _logger = logger;
        _watchPaths = watchPaths is { Length: > 0 } ? watchPaths : DefaultWatchPaths;
        _ransomwareDetector = ransomwareDetector ?? new RansomwareDetector();
        _ransomwareDetector.AlarmRaised += OnRansomwareAlarm;
    }

    public void Start()
    {
        if (_running) return;

        foreach (var path in _watchPaths)
        {
            if (!Directory.Exists(path)) continue;

            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            watcher.Created += OnFileEvent;
            watcher.Changed += OnFileEvent;
            watcher.Renamed += OnRenamed;
            _watchers.Add(watcher);
        }

        _running = true;
        _logger?.LogInformation("RealTimeGuard started, watching {Count} directories", _watchers.Count);
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        _running = false;
        _logger?.LogInformation("RealTimeGuard stopped");
    }

    public static bool IsMonitoredExtension(string fileName) =>
        MonitoredExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (!IsMonitoredExtension(e.FullPath)) return;
        _ = ScanAndActAsync(e.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _ransomwareDetector.RecordRename(e.OldFullPath, e.FullPath);
        if (!IsMonitoredExtension(e.FullPath)) return;
        _ = ScanAndActAsync(e.FullPath);
    }

    private void OnRansomwareAlarm(object? sender, EventArgs e)
    {
        _logger?.LogWarning("Ransomware mass-rename detected — alarm triggered");
        ThreatDetected?.Invoke(this, "[RANSOMWARE] Mass file rename detected");
    }

    private async Task ScanAndActAsync(string filePath)
    {
        try
        {
            await Task.Delay(200);
            var result = await _scanEngine.ScanFileAsync(filePath);
            if (result.IsThreat)
            {
                _logger?.LogWarning("Real-time threat detected: {Path} ({Threat})", filePath, result.ThreatName);
                await IsolateAsync(filePath, result);
                ThreatDetected?.Invoke(this, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error scanning file {Path}", filePath);
        }
    }

    private async Task IsolateAsync(string filePath, Engine.Models.ScanResult result, CancellationToken ct = default)
    {
        if (_quarantineDirect is not null)
        {
            await _quarantineDirect.IsolateAsync(filePath, result, ct);
            return;
        }

        await using var scope = _scopeFactory!.CreateAsyncScope();
        var quarantine = scope.ServiceProvider.GetRequiredService<IQuarantineManager>();
        await quarantine.IsolateAsync(filePath, result, ct);
    }
}
