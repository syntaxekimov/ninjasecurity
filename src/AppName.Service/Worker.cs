using AppName.Service.Ipc;

namespace AppName.Service;

public class Worker : BackgroundService
{
    private readonly IpcServer _ipcServer;
    private readonly ILogger<Worker> _logger;

    public Worker(IpcServer ipcServer, ILogger<Worker> logger)
    {
        _ipcServer = ipcServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppName Security Service started at: {time}", DateTimeOffset.Now);
        await _ipcServer.RunAsync(stoppingToken);
        _logger.LogInformation("AppName Security Service stopped.");
    }
}
