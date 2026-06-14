using AppName.Service.Engine.Interfaces;
using System.Text.Json;

namespace AppName.Service.Ipc;

public class CommandHandler
{
    private readonly IScanEngine _scanEngine;
    private readonly IQuarantineManager _quarantine;
    private readonly IRealTimeGuard _realTimeGuard;
    private readonly IProcessMonitor _processMonitor;
    private readonly ISystemOptimizer _systemOptimizer;
    private readonly IUpdateService _updateService;

    public CommandHandler(
        IScanEngine scanEngine,
        IQuarantineManager quarantine,
        IRealTimeGuard realTimeGuard,
        IProcessMonitor processMonitor,
        ISystemOptimizer systemOptimizer,
        IUpdateService updateService)
    {
        _scanEngine = scanEngine;
        _quarantine = quarantine;
        _realTimeGuard = realTimeGuard;
        _processMonitor = processMonitor;
        _systemOptimizer = systemOptimizer;
        _updateService = updateService;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct = default)
    {
        return request.Command switch
        {
            "StartScan"          => await HandleStartScanAsync(request, ct),
            "GetStatus"          => HandleGetStatus(),
            "QuarantineAction"   => await HandleQuarantineActionAsync(request, ct),
            "ListQuarantine"     => await HandleListQuarantineAsync(ct),
            "GetRealTimeStatus"  => HandleGetRealTimeStatus(),
            "SetRealTimeEnabled" => HandleSetRealTimeEnabled(request),
            "GetProcessList"     => await HandleGetProcessListAsync(ct),
            "GetAutostartEntries"=> await HandleGetAutostartAsync(ct),
            "CleanTempFiles"     => await HandleCleanTempAsync(ct),
            "UpdateSignatures"   => await HandleUpdateSignaturesAsync(ct),
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

    private IpcResponse HandleGetRealTimeStatus() =>
        IpcResponse.Ok(JsonSerializer.SerializeToElement(new
        {
            enabled = _realTimeGuard.IsRunning
        }));

    private IpcResponse HandleSetRealTimeEnabled(IpcRequest request)
    {
        if (request.Payload is not JsonElement payload)
            return IpcResponse.Fail("Payload required");
        var args = payload.Deserialize<SetRealTimePayload>();
        if (args is null) return IpcResponse.Fail("Invalid payload");
        if (args.Enabled) _realTimeGuard.Start(); else _realTimeGuard.Stop();
        return IpcResponse.Ok();
    }

    private async Task<IpcResponse> HandleGetProcessListAsync(CancellationToken ct)
    {
        var processes = await _processMonitor.GetProcessesAsync(ct);
        return IpcResponse.Ok(JsonSerializer.SerializeToElement(processes.Select(p => new
        {
            p.Pid, p.Name, p.ExecutablePath, p.HasValidSignature, p.RiskScore
        })));
    }

    private async Task<IpcResponse> HandleGetAutostartAsync(CancellationToken ct)
    {
        var entries = await _systemOptimizer.GetAutostartEntriesAsync(ct);
        return IpcResponse.Ok(JsonSerializer.SerializeToElement(entries));
    }

    private async Task<IpcResponse> HandleCleanTempAsync(CancellationToken ct)
    {
        var freed = await _systemOptimizer.CleanTempFilesAsync(ct);
        return IpcResponse.Ok(JsonSerializer.SerializeToElement(new { freedBytes = freed }));
    }

    private async Task<IpcResponse> HandleUpdateSignaturesAsync(CancellationToken ct)
    {
        var ok = await _updateService.UpdateSignaturesAsync(ct);
        return ok ? IpcResponse.Ok() : IpcResponse.Fail("Signature update failed");
    }
}
