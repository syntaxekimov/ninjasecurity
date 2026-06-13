using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AppName.Service.Engine;

public class ProcessMonitor : IProcessMonitor
{
    private static readonly HashSet<string> LolBins =
    [
        "powershell.exe", "cmd.exe", "wscript.exe", "cscript.exe",
        "mshta.exe", "regsvr32.exe", "certutil.exe", "bitsadmin.exe",
        "rundll32.exe", "msiexec.exe", "wmic.exe"
    ];

    private static readonly string[] SuspiciousPaths =
    [
        Path.GetTempPath().ToLowerInvariant(),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData).ToLowerInvariant(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads").ToLowerInvariant(),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData).ToLowerInvariant(),
    ];

    private readonly IScanEngine _scanEngine;
    private readonly ILogger<ProcessMonitor>? _logger;
    private readonly HashSet<int> _knownPids = [];
    private CancellationTokenSource? _cts;
    private bool _running;

    public bool IsRunning => _running;

    public ProcessMonitor(IScanEngine scanEngine, ILogger<ProcessMonitor>? logger = null)
    {
        _scanEngine = scanEngine;
        _logger = logger;
    }

    public void Start()
    {
        if (_running) return;
        _cts = new CancellationTokenSource();
        _running = true;
        _ = PollLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _running = false;
    }

    public Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken ct = default)
    {
        var processes = Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    var path = p.MainModule?.FileName;
                    var hasSig = CheckSignature(path);
                    return new ProcessInfo(
                        p.Id,
                        p.ProcessName,
                        path,
                        hasSig,
                        ComputeRiskScore(path ?? "", hasSig),
                        p.StartTime);
                }
                catch { return null; }
            })
            .Where(p => p != null)
            .Cast<ProcessInfo>()
            .ToList();

        return Task.FromResult<IReadOnlyList<ProcessInfo>>(processes);
    }

    public static int ComputeRiskScore(string executablePath, bool hasSig)
    {
        var score = 0;
        var lower = executablePath.ToLowerInvariant();

        if (!hasSig) score += 30;

        foreach (var suspPath in SuspiciousPaths)
        {
            if (!string.IsNullOrEmpty(suspPath) && lower.StartsWith(suspPath))
            {
                score += 25;
                break;
            }
        }

        if (lower.Contains("downloads")) score += 15;

        if (IsLolBin(Path.GetFileName(lower))) score += 35;

        return Math.Min(score, 100);
    }

    public static bool IsLolBin(string processName) =>
        LolBins.Contains(processName.ToLowerInvariant());

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckNewProcessesAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ProcessMonitor poll error");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task CheckNewProcessesAsync(CancellationToken ct)
    {
        var current = Process.GetProcesses();
        var currentPids = current.Select(p => p.Id).ToHashSet();
        var newPids = currentPids.Except(_knownPids).ToList();

        foreach (var pid in newPids)
        {
            _knownPids.Add(pid);
            var proc = current.FirstOrDefault(p => p.Id == pid);
            if (proc is null) continue;

            try
            {
                var path = proc.MainModule?.FileName;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                var result = await _scanEngine.ScanFileAsync(path, ct);
                if (result.IsThreat)
                    _logger?.LogWarning("New process threat: PID={Pid} Path={Path}", pid, path);
            }
            catch { }
        }

        _knownPids.IntersectWith(currentPids);
    }

    private static bool CheckSignature(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        try
        {
            var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
            return cert != null;
        }
        catch { return false; }
    }
}
