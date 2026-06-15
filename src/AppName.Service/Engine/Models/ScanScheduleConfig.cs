namespace NinjaSecurity.Service.Engine.Models;

public record ScanScheduleConfig
{
    public bool Enabled { get; init; } = false;
    public string ScanType { get; init; } = "Quick";
    public int IntervalHours { get; init; } = 24;
    public DateTime? LastRunUtc { get; init; }
}
