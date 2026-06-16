using System.Text.Json;

namespace NinjaSecurity.Service.Ipc;

public record IpcRequest(string Command, JsonElement? Payload = null);

public record IpcResponse(bool Success, string? Error, JsonElement? Data)
{
    public static IpcResponse Ok(JsonElement? data = null) =>
        new(true, null, data);

    public static IpcResponse Fail(string error) =>
        new(false, error, null);
}

public record StartScanPayload(string Type, string? Path);
public record QuarantineActionPayload(Guid Id, string Action);

public record IpcEvent(string EventType, JsonElement? Data = null);

public record ScanProgressPayload(int Percent, string CurrentFile, int FilesScanned);
public record ThreatFoundPayload(string FilePath, string? ThreatName, int ConfidenceScore);
public record ScanCompletePayload(int ThreatsFound, int FilesScanned, TimeSpan Duration);
public record RealTimeAlertPayload(string FilePath, string? ThreatName, bool AutoQuarantined);

public record SetRealTimePayload(bool Enabled);

public record SetScanSchedulePayload(bool Enabled, string ScanType, int IntervalHours);
public record SetAutostartPayload(string EntryId, bool Enabled);
