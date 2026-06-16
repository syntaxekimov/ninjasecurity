using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.Principal;
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
        await using var pipe = CreatePipe();
        await pipe.WaitForConnectionAsync(ct);

        var callerIsAdmin = CheckCallerIsAdmin(pipe);
        if (!callerIsAdmin)
            _logger.LogWarning("Security: non-admin process connected to {PipeName}", PipeName);

        var buffer = new byte[65536];
        var bytesRead = await pipe.ReadAsync(buffer, ct);
        var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        IpcResponse response;
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CommandHandler>();

        try
        {
            var request = JsonSerializer.Deserialize<IpcRequest>(json)
                ?? throw new InvalidOperationException("Null request");
            response = await handler.HandleAsync(request, callerIsAdmin, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing IPC request");
            response = IpcResponse.Fail("Internal error");
        }

        var responseJson = JsonSerializer.Serialize(response);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
        await pipe.WriteAsync(responseBytes, ct);
    }

    private static NamedPipeServerStream CreatePipe()
    {
        if (OperatingSystem.IsWindows())
            return CreateSecurePipe();

        return new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);
    }

    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreateSecurePipe() =>
        NamedPipeServerStreamAcl.Create(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous,
            inBufferSize:  0,
            outBufferSize: 0,
            IpcPipeSecurity.CreateAdminOnlySecurity());

    private bool CheckCallerIsAdmin(NamedPipeServerStream pipe)
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            return CheckCallerIsAdminWindows(pipe);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not determine caller identity on {PipeName}", PipeName);
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool CheckCallerIsAdminWindows(NamedPipeServerStream pipe)
    {
        bool isAdmin = false;
        pipe.RunAsClient(() =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            isAdmin = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        });
        return isAdmin;
    }
}
