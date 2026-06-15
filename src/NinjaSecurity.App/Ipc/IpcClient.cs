using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NinjaSecurity.App.Ipc;

public class IpcClient
{
    public const string PipeName = "NinjaSecurity.IPC";

    public async Task<IpcResponse> SendAsync(
        string command,
        object? payload = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(3000, ct);

            var request = new IpcRequest(
                command,
                payload is null ? null : JsonSerializer.SerializeToElement(payload));

            var requestJson = JsonSerializer.Serialize(request);
            var requestBytes = Encoding.UTF8.GetBytes(requestJson);
            await pipe.WriteAsync(requestBytes, ct);

            var buffer = new byte[65536];
            var read = await pipe.ReadAsync(buffer, ct);
            var responseJson = Encoding.UTF8.GetString(buffer, 0, read);
            return JsonSerializer.Deserialize<IpcResponse>(responseJson)
                   ?? new IpcResponse(false, "Empty response", null);
        }
        catch (Exception ex)
        {
            return new IpcResponse(false, ex.Message, null);
        }
    }

    public async Task<T?> GetDataAsync<T>(
        string command,
        object? payload = null,
        CancellationToken ct = default)
    {
        var response = await SendAsync(command, payload, ct);
        if (!response.Success || response.Data is null) return default;
        return response.Data.Value.Deserialize<T>();
    }
}
