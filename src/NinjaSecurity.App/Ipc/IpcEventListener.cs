using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NinjaSecurity.App.Ipc;

public class IpcEventListener
{
    public const string EventPipeName = "NinjaSecurity.Events";

    public event EventHandler<IpcEvent>? EventReceived;

    private CancellationTokenSource? _cts;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = ListenLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".", EventPipeName, PipeDirection.In,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(5000, ct);

                using var reader = new StreamReader(pipe, Encoding.UTF8);
                while (!ct.IsCancellationRequested && pipe.IsConnected)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break;
                    var evt = JsonSerializer.Deserialize<IpcEvent>(line);
                    if (evt is not null)
                        EventReceived?.Invoke(this, evt);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(2000, ct); }
        }
    }
}
