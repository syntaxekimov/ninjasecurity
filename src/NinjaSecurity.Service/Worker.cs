using NinjaSecurity.Service.Engine;
using NinjaSecurity.Service.Engine.Interfaces;
using NinjaSecurity.Service.Ipc;
using System.Text.Json;

namespace NinjaSecurity.Service;

public class Worker : BackgroundService
{
    private readonly IpcServer _ipcServer;
    private readonly IpcEventChannel _eventChannel;
    private readonly IRealTimeGuard _realTimeGuard;
    private readonly IProcessMonitor _processMonitor;
    private readonly ScanScheduler _scheduler;
    private readonly IClamAvDaemon _clamAvDaemon;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IpcServer ipcServer,
        IpcEventChannel eventChannel,
        IRealTimeGuard realTimeGuard,
        IProcessMonitor processMonitor,
        ScanScheduler scheduler,
        IClamAvDaemon clamAvDaemon,
        ILogger<Worker> logger)
    {
        _ipcServer     = ipcServer;
        _eventChannel  = eventChannel;
        _realTimeGuard = realTimeGuard;
        _processMonitor = processMonitor;
        _scheduler     = scheduler;
        _clamAvDaemon  = clamAvDaemon;
        _logger        = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ninja Security Service starting...");

        _realTimeGuard.ThreatDetected += OnThreatDetected;
        _realTimeGuard.Start();
        _processMonitor.Start();

        await Task.WhenAll(
            _ipcServer.RunAsync(stoppingToken),
            _eventChannel.RunAsync(stoppingToken),
            _scheduler.RunAsync(stoppingToken),
            _clamAvDaemon.RunAsync(stoppingToken)
        );

        _realTimeGuard.ThreatDetected -= OnThreatDetected;
        _realTimeGuard.Stop();
        _processMonitor.Stop();

        _logger.LogInformation("Ninja Security Service stopped.");
    }

    private void OnThreatDetected(object? sender, string filePath) =>
        _eventChannel.Publish(new IpcEvent("ThreatFound",
            JsonSerializer.SerializeToElement(new ThreatFoundPayload(filePath, null, 0))));
}
