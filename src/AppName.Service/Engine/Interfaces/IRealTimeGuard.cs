namespace NinjaSecurity.Service.Engine.Interfaces;

public interface IRealTimeGuard
{
    bool IsRunning { get; }
    void Start();
    void Stop();
    event EventHandler<string>? ThreatDetected;
}
