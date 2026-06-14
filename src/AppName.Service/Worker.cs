using AppName.Service.Engine.Interfaces;
using AppName.Service.Ipc;
using System.Text.Json;

namespace AppName.Service;

public class Worker : BackgroundService
{
    private readonly IpcServer _ipcServer;
    private readonly IpcEventChannel _eventChannel;
    private readonly IRealTimeGuard _realTimeGuard;
    private readonly IProcessMonitor _processMonitor;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IpcServer ipcServer,
        IpcEventChannel eventChannel,
        IRealTimeGuard realTimeGuard,
        IProcessMonitor processMonitor,
        ILogger<Worker> logger)
    {
        _ipcServer = ipcServer;
        _eventChannel = eventChannel;
        _realTimeGuard = realTimeGuard;
        _processMonitor = processMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppName Security Service starting...");

        _realTimeGuard.ThreatDetected += OnThreatDetected;
        _realTimeGuard.Start();
        _processMonitor.Start();

        await Task.WhenAll(
            _ipcServer.RunAsync(stoppingToken),
            _eventChannel.RunAsync(stoppingToken)
        );

        _realTimeGuard.ThreatDetected -= OnThreatDetected;
        _realTimeGuard.Stop();
        _processMonitor.Stop();

        _logger.LogInformation("AppName Security Service stopped.");
    }

    private void OnThreatDetected(object? sender, string filePath) =>
        _eventChannel.Publish(new IpcEvent("ThreatFound",
            JsonSerializer.SerializeToElement(new ThreatFoundPayload(filePath, null, 0))));
}
