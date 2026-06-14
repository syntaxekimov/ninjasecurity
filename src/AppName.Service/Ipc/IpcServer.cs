using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NinjaSecurity.Service.Ipc;

public class IpcServer
{
    public const string PipeName = "NinjaSecurity.IPC";

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
        // TODO(Plan 2 - security): restrict pipe ACL to Administrators + SYSTEM via
        // NamedPipeServerStreamAcl.Create (Windows-only). Current state: any local
        // user can connect. Mitigated in production by running the service as SYSTEM
        // and requiring the GUI to run elevated.
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
            // Log full exception server-side, return opaque error to client
            _logger.LogError(ex, "Unhandled error processing IPC request");
            response = IpcResponse.Fail("Internal error");
        }

        var responseJson = JsonSerializer.Serialize(response);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
        await pipe.WriteAsync(responseBytes, ct);
    }
}
