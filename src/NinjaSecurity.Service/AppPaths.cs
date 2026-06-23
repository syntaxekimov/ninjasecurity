using Microsoft.Win32;
using System.Runtime.Versioning;

namespace NinjaSecurity.Service;

public static class AppPaths
{
    // Mutable data — lives in CommonApplicationData (all users)
    public static readonly string AppData =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                     "NinjaSecurity");

    public static readonly string DatabasePath    = Path.Combine(AppData, "ninjasecurity.db");
    public static readonly string QuarantinePath  = Path.Combine(AppData, "Quarantine");
    public static readonly string ClamDbPath      = Path.Combine(AppData, "clamav-db");

    // Install-time assets — resolved from registry (set by NSIS) or process location
    public static readonly string InstallPath     = ResolveInstallPath();
    public static readonly string ClamAvPath      = Path.Combine(InstallPath, "clamav");
    public static readonly string ClamdExe        = Path.Combine(ClamAvPath, "clamd.exe");
    public static readonly string FreshclamExe    = Path.Combine(ClamAvPath, "freshclam.exe");
    // Config files written at runtime to ProgramData so SYSTEM can always update them
    public static readonly string ClamdConfig     = Path.Combine(AppData, "clamd.conf");
    public static readonly string FreshclamConfig = Path.Combine(AppData, "freshclam.conf");
    public static readonly string RulesPath       = Path.Combine(InstallPath, "rules");
    public static readonly string HashDbPath      = Path.Combine(InstallPath, "hashes.db");

    // Legacy aliases kept so existing callers compile unchanged
    public static string DatabasesPath  => Path.Combine(AppData, "Databases");
    public static string YaraRulesPath  => RulesPath;
    public static string MalSearcherDbPath => HashDbPath;

    private static string ResolveInstallPath()
    {
        if (OperatingSystem.IsWindows())
        {
            try
            {
#pragma warning disable CA1416
                using var key = Registry.LocalMachine.OpenSubKey(@"Software\Ninja Security");
                if (key?.GetValue("InstallDir") is string dir && !string.IsNullOrEmpty(dir))
                    return dir;
#pragma warning restore CA1416
            }
            catch { }

            // Fallback: service lives in $INSTDIR\service\, so go up one level
            var processDir = Path.GetDirectoryName(Environment.ProcessPath ?? "");
            if (!string.IsNullOrEmpty(processDir))
                return Path.GetDirectoryName(processDir) ?? processDir;
        }

        return AppContext.BaseDirectory;
    }
}
