using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface IProcessMonitor
{
    bool IsRunning { get; }
    void Start();
    void Stop();
    Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken ct = default);
}
