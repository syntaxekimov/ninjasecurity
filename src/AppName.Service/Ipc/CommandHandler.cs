using AppName.Service.Engine.Interfaces;
using System.Text.Json;

namespace AppName.Service.Ipc;

public class CommandHandler
{
    private readonly IScanEngine _scanEngine;
    private readonly IQuarantineManager _quarantine;

    public CommandHandler(IScanEngine scanEngine, IQuarantineManager quarantine)
    {
        _scanEngine = scanEngine;
        _quarantine = quarantine;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct = default)
    {
        return request.Command switch
        {
            "StartScan"        => await HandleStartScanAsync(request, ct),
            "GetStatus"        => HandleGetStatus(),
            "QuarantineAction" => await HandleQuarantineActionAsync(request, ct),
            "ListQuarantine"   => await HandleListQuarantineAsync(ct),
            _ => IpcResponse.Fail($"Unknown command: {request.Command}")
        };
    }

    private async Task<IpcResponse> HandleStartScanAsync(IpcRequest request, CancellationToken ct)
    {
        if (request.Payload is not JsonElement payload)
            return IpcResponse.Fail("Payload required");

        var args = payload.Deserialize<StartScanPayload>();
        if (args is null)
            return IpcResponse.Fail("Invalid payload");

        var path = args.Type switch
        {
            "Quick"  => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Full"   => Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\",
            "Custom" => args.Path ?? "",
            _ => ""
        };

        if (string.IsNullOrEmpty(path))
            return IpcResponse.Fail($"Unknown scan type: {args.Type}");

        var results = await _scanEngine.ScanDirectoryAsync(path, recursive: true, ct);
        return IpcResponse.Ok(JsonSerializer.SerializeToElement(new
        {
            threatsFound = results.Count,
            threats = results.Select(r => new { r.FilePath, r.ThreatName, r.ConfidenceScore })
        }));
    }

    private static IpcResponse HandleGetStatus() =>
        IpcResponse.Ok(JsonSerializer.SerializeToElement(new { status = "running", protected_ = true }));

    private async Task<IpcResponse> HandleQuarantineActionAsync(IpcRequest request, CancellationToken ct)
    {
        if (request.Payload is not JsonElement payload)
            return IpcResponse.Fail("Payload required");

        var args = payload.Deserialize<QuarantineActionPayload>();
        if (args is null) return IpcResponse.Fail("Invalid payload");

        try
        {
            if (args.Action == "Restore")
                await _quarantine.RestoreAsync(args.Id, ct);
            else if (args.Action == "Delete")
                await _quarantine.DeleteAsync(args.Id, ct);
            else
                return IpcResponse.Fail($"Unknown action: {args.Action}");

            return IpcResponse.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return IpcResponse.Fail(ex.Message);
        }
    }

    private async Task<IpcResponse> HandleListQuarantineAsync(CancellationToken ct)
    {
        var entries = await _quarantine.ListAsync(ct);
        return IpcResponse.Ok(JsonSerializer.SerializeToElement(entries.Select(e => new
        {
            e.Id,
            e.OriginalPath,
            e.ThreatName,
            e.ConfidenceScore,
            e.Sha256,
            QuarantinedAt = e.QuarantinedAt.ToString("O")
        })));
    }
}
