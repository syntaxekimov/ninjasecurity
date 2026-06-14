using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace NinjaSecurity.Service.Ipc;

public class IpcEventChannel
{
    public const string EventPipeName = "NinjaSecurity.Events";

    private readonly ILogger<IpcEventChannel> _logger;
    private readonly Channel<IpcEvent> _queue;

    public IpcEventChannel(ILogger<IpcEventChannel> logger)
    {
        _logger = logger;
        _queue = Channel.CreateUnbounded<IpcEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Publish(IpcEvent evt) => _queue.Writer.TryWrite(evt);

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await AcceptAndStreamAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event channel connection lost, restarting...");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task AcceptAndStreamAsync(CancellationToken ct)
    {
        // TODO(Plan 2 - security): apply PipeSecurity ACL (Administrators + SYSTEM only)
        await using var pipe = new NamedPipeServerStream(
            EventPipeName,
            PipeDirection.Out,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _logger.LogInformation("Event channel waiting for GUI connection...");
        await pipe.WaitForConnectionAsync(ct);
        _logger.LogInformation("GUI connected to event channel");

        await foreach (var evt in _queue.Reader.ReadAllAsync(ct))
        {
            if (!pipe.IsConnected) break;
            var json = JsonSerializer.Serialize(evt) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes, ct);
            await pipe.FlushAsync(ct);
        }
    }
}
