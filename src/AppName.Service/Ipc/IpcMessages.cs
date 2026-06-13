using System.Text.Json;

namespace AppName.Service.Ipc;

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
