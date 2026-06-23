namespace NinjaSecurity.Service.Engine.Interfaces;

public interface IClamAvDaemon
{
    bool IsRunning { get; }
    Task RunAsync(CancellationToken ct);
    Task UpdateDefinitionsAsync(CancellationToken ct = default);
}
