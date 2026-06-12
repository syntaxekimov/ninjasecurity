namespace AppName.Service.Data.Entities;

public class ScanHistoryEntry
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ScanType { get; set; } = "";
    public int FilesScanned { get; set; }
    public int ThreatsFound { get; set; }
    public string? ScanPath { get; set; }
}
