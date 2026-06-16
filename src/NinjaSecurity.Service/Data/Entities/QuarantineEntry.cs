namespace NinjaSecurity.Service.Data.Entities;

public class QuarantineEntry
{
    public Guid Id { get; set; }
    public string OriginalPath { get; set; } = "";
    public string ThreatName { get; set; } = "";
    public int ConfidenceScore { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTime QuarantinedAt { get; set; }
    public string EncryptedFileName { get; set; } = "";
    public byte[] ProtectedKey { get; set; } = [];
    public byte[] Iv { get; set; } = [];
}
