using NinjaSecurity.Service.Engine.Interfaces;
using NinjaSecurity.Service.Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace NinjaSecurity.Service.Engine;

public class SystemOptimizer : ISystemOptimizer
{
    private static readonly string[] AutorunRegistryPaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
    ];

    private readonly IScanEngine _scanEngine;
    private readonly ILogger<SystemOptimizer>? _logger;

    public SystemOptimizer(IScanEngine scanEngine, ILogger<SystemOptimizer>? logger = null)
    {
        _scanEngine = scanEngine;
        _logger = logger;
    }

    public Task<IReadOnlyList<AutorunEntry>> GetAutostartEntriesAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return Task.FromResult<IReadOnlyList<AutorunEntry>>([]);

        var entries = new List<AutorunEntry>();
        entries.AddRange(GetRegistryEntries());
        entries.AddRange(GetStartupFolderEntries());
        return Task.FromResult<IReadOnlyList<AutorunEntry>>(entries);
    }

    public Task SetAutostartEnabledAsync(string entryId, bool enabled, CancellationToken ct = default)
    {
        _logger?.LogInformation("SetAutostartEnabled {Id}={Enabled} (not yet implemented)", entryId, enabled);
        return Task.CompletedTask;
    }

    public Task<long> GetTempFileSizeAsync(CancellationToken ct = default)
    {
        var size = GetDirectorySize(Path.GetTempPath());
        return Task.FromResult(size);
    }

    public Task<long> CleanTempFilesAsync(CancellationToken ct = default)
    {
        long freed = 0;
        var tempPath = Path.GetTempPath();
        foreach (var file in Directory.EnumerateFiles(tempPath, "*", RecursiveOpts))
        {
            try
            {
                var info = new FileInfo(file);
                freed += info.Length;
                File.Delete(file);
            }
            catch { }
        }
        _logger?.LogInformation("Cleaned {Freed} bytes from temp", freed);
        return Task.FromResult(freed);
    }

    public static int ComputeEntryRisk(string? imagePath, bool isSigned, string location)
    {
        var score = 0;
        if (!isSigned) score += 30;

        if (!string.IsNullOrEmpty(imagePath))
        {
            var lower = imagePath.ToLowerInvariant();
            if (lower.Contains("\\temp\\") || lower.StartsWith(Path.GetTempPath().ToLowerInvariant()))
                score += 25;
            else if (lower.Contains("\\appdata\\roaming\\"))
                score += 15;
            if (lower.Contains("powershell") || lower.Contains("wscript") ||
                lower.Contains("mshta") || lower.Contains("certutil") ||
                lower.Contains("regsvr32") || lower.Contains("bitsadmin"))
                score += 35;
        }

        return Math.Min(score, 100);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private IEnumerable<AutorunEntry> GetRegistryEntries()
    {
        var entries = new List<AutorunEntry>();
        foreach (var regPath in AutorunRegistryPaths)
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = hive.OpenSubKey(regPath, writable: false);
                    if (key is null) continue;
                    foreach (var name in key.GetValueNames())
                    {
                        var imagePath = key.GetValue(name)?.ToString();
                        var isSigned = CheckSignature(imagePath);
                        entries.Add(new AutorunEntry(
                            Id: $"reg:{hive.Name}\\{regPath}\\{name}",
                            Name: name,
                            ImagePath: imagePath,
                            Location: $"Registry\\{Path.GetFileName(regPath)}",
                            IsEnabled: true,
                            IsSigned: isSigned,
                            RiskScore: ComputeEntryRisk(imagePath, isSigned, regPath)
                        ));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Registry access error for {Path}", regPath);
                }
            }
        }
        return entries;
    }

    private static IEnumerable<AutorunEntry> GetStartupFolderEntries()
    {
        var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (!Directory.Exists(startupPath)) yield break;
        foreach (var file in Directory.EnumerateFiles(startupPath))
        {
            var isSigned = CheckSignature(file);
            yield return new AutorunEntry(
                Id: $"startup:{file}",
                Name: Path.GetFileName(file),
                ImagePath: file,
                Location: "StartupFolder",
                IsEnabled: true,
                IsSigned: isSigned,
                RiskScore: ComputeEntryRisk(file, isSigned, "StartupFolder")
            );
        }
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

    private static readonly EnumerationOptions RecursiveOpts = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible    = true,
        AttributesToSkip      = FileAttributes.ReparsePoint
    };

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return Directory.EnumerateFiles(path, "*", RecursiveOpts)
            .Sum(f =>
            {
                try { return new FileInfo(f).Length; }
                catch { return 0L; }
            });
    }
}
