using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NinjaSecurity.Service.Engine.Interfaces;

namespace NinjaSecurity.Service.Engine;

public sealed class ClamAvDaemon : IClamAvDaemon, IAsyncDisposable
{
    private readonly ILogger<ClamAvDaemon> _logger;
    private Process? _process;
    private volatile bool _isRunning;

    public bool IsRunning => _isRunning;

    public ClamAvDaemon(ILogger<ClamAvDaemon> logger)
    {
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        if (!File.Exists(AppPaths.ClamdExe))
        {
            _logger.LogWarning("ClamAV not found at {Path} — virus scanning disabled", AppPaths.ClamdExe);
            await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            return;
        }

        EnsureDirectories();
        WriteConfigFiles();

        if (!HasDatabase())
        {
            _logger.LogInformation("No ClamAV database — downloading signatures (may take several minutes)...");
            try
            {
                await UpdateDefinitionsAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClamAV signature download failed — scanning disabled until next update");
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
                return;
            }
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await StartClamdAsync(ct).ConfigureAwait(false);
                if (_process is not null)
                    await _process.WaitForExitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "clamd error");
            }
            finally
            {
                _isRunning = false;
            }

            if (ct.IsCancellationRequested) break;
            _logger.LogWarning("clamd stopped unexpectedly — restarting in 10 seconds...");
            try { await Task.Delay(10_000, ct).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }

        await StopClamdAsync().ConfigureAwait(false);
    }

    public async Task UpdateDefinitionsAsync(CancellationToken ct = default)
    {
        if (!File.Exists(AppPaths.FreshclamExe))
        {
            _logger.LogWarning("freshclam.exe not found — cannot update signatures");
            return;
        }

        _logger.LogInformation("Running freshclam to update ClamAV signatures...");
        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo
        {
            FileName               = AppPaths.FreshclamExe,
            Arguments              = $"--config-file=\"{AppPaths.FreshclamConfig}\" --no-warnings",
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true
        };

        proc.Start();
        var stdoutTask = proc.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct).ConfigureAwait(false);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        if (!string.IsNullOrWhiteSpace(stdout)) _logger.LogInformation("[freshclam] {Out}", stdout.Trim());
        if (!string.IsNullOrWhiteSpace(stderr))  _logger.LogWarning("[freshclam] {Err}", stderr.Trim());

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"freshclam exited with code {proc.ExitCode}");

        _logger.LogInformation("ClamAV signatures updated");
    }

    private async Task StartClamdAsync(CancellationToken ct)
    {
        _process?.Dispose();
        _process = null;

        _logger.LogInformation("Starting clamd...");
        var p = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName               = AppPaths.ClamdExe,
                Arguments              = $"--config-file=\"{AppPaths.ClamdConfig}\"",
                UseShellExecute        = false,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                CreateNoWindow         = true
            },
            EnableRaisingEvents = true
        };

        p.OutputDataReceived += (_, e) => { if (e.Data is not null) _logger.LogDebug("[clamd] {Line}", e.Data); };
        p.ErrorDataReceived  += (_, e) => { if (e.Data is not null) _logger.LogWarning("[clamd] {Line}", e.Data); };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        _process   = p;
        _isRunning = true;

        // Give clamd time to bind port 3310 before returning
        await Task.Delay(5_000, ct).ConfigureAwait(false);
        _logger.LogInformation("clamd started (PID {Pid})", p.Id);
    }

    private Task StopClamdAsync()
    {
        var p = _process;
        if (p is null || p.HasExited) return Task.CompletedTask;
        try { p.Kill(entireProcessTree: true); }
        catch { }
        _isRunning = false;
        _logger.LogInformation("clamd stopped");
        return Task.CompletedTask;
    }

    private static void EnsureDirectories() =>
        Directory.CreateDirectory(AppPaths.ClamDbPath);

    private static bool HasDatabase() =>
        Directory.Exists(AppPaths.ClamDbPath)
        && (Directory.EnumerateFiles(AppPaths.ClamDbPath, "*.cvd").Any()
            || Directory.EnumerateFiles(AppPaths.ClamDbPath, "*.cld").Any());

    private static void WriteConfigFiles()
    {
        var dbPath  = AppPaths.ClamDbPath;
        var logPath = Path.Combine(AppPaths.AppData, "clamd.log");
        var pidPath = Path.Combine(AppPaths.AppData, "clamd.pid");

        File.WriteAllText(AppPaths.ClamdConfig, $"""
            TCPSocket 3310
            TCPAddr 127.0.0.1
            DatabaseDirectory {dbPath}
            LogFile {logPath}
            PidFile {pidPath}
            MaxRecursion 16
            MaxFileSize 25M
            MaxScanSize 100M
            ScanPE yes
            ScanELF yes
            ScanOLE2 yes
            ScanPDF yes
            ScanHTML yes
            ScanArchive yes
            """);

        File.WriteAllText(AppPaths.FreshclamConfig, $"""
            DatabaseDirectory {dbPath}
            UpdateLogFile {Path.Combine(AppPaths.AppData, "freshclam.log")}
            DatabaseMirror database.clamav.net
            Checks 12
            """);
    }

    public async ValueTask DisposeAsync()
    {
        await StopClamdAsync();
        _process?.Dispose();
    }
}
