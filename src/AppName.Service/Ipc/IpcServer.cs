using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AppName.Service.Ipc;

public class IpcServer
{
    public const string PipeName = "AppName.Security.IPC";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IpcServer> _logger;

    public IpcServer(IServiceScopeFactory scopeFactory, ILogger<IpcServer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("IPC server starting on pipe: {PipeName}", PipeName);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await AcceptConnectionAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IPC connection error");
            }
        }
    }

    private async Task AcceptConnectionAsync(CancellationToken ct)
    {
        await using var pipe = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);

        await pipe.WaitForConnectionAsync(ct);

        var buffer = new byte[65536];
        var bytesRead = await pipe.ReadAsync(buffer, ct);
        var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        IpcResponse response;
        // Create a DI scope per request so scoped DbContext is properly managed
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CommandHandler>();

        try
        {
            var request = JsonSerializer.Deserialize<IpcRequest>(json)
                ?? throw new InvalidOperationException("Null request");
            response = await handler.HandleAsync(request, ct);
        }
        catch (Exception ex)
        {
            response = IpcResponse.Fail(ex.Message);
        }

        var responseJson = JsonSerializer.Serialize(response);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
        await pipe.WriteAsync(responseBytes, ct);
    }
}
