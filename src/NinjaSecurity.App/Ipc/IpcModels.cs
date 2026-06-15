using System.Text.Json;

namespace NinjaSecurity.App.Ipc;

public record IpcRequest(string Command, JsonElement? Payload = null);
public record IpcResponse(bool Success, string? Error, JsonElement? Data);
public record IpcEvent(string EventType, JsonElement? Data = null);
