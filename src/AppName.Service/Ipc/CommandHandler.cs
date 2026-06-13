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
            "Custom" => ValidateCustomPath(args.Path),
            _ => null
        };

        if (path is null)
            return IpcResponse.Fail($"Unknown scan type: {args.Type}");
        if (string.IsNullOrEmpty(path))
            return IpcResponse.Fail("Custom scan requires a valid local path");

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
        catch (InvalidOperationException)
        {
            return IpcResponse.Fail("Quarantine entry not found");
        }
    }

    // TODO(Plan 2 - security): gate Restore/Delete/Custom scan behind admin check via
    // WindowsPrincipal.IsInRole(WindowsBuiltInRole.Administrator) using caller identity
    // obtained from pipe.RunAsClient() in IpcServer. Write audit log entry with caller SID.

    private static string? ValidateCustomPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "";
        // Reject UNC paths and non-rooted paths to prevent traversal to network shares
        var full = Path.GetFullPath(path);
        if (full.StartsWith(@"\\") || !Path.IsPathRooted(full))
            return "";
        return full;
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
