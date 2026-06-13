namespace AppName.Service.Engine.Models;

public record ProcessInfo(
    int Pid,
    string Name,
    string? ExecutablePath,
    bool HasValidSignature,
    int RiskScore,
    DateTime StartTime
);
