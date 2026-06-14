using NinjaSecurity.Service.Engine.Interfaces;
using System.Security.Cryptography;

namespace NinjaSecurity.Service.Engine;

// Windows-only: uses DPAPI to protect AES keys at rest.
// Falls back to no-op on non-Windows (dev/CI only — not for production use).
public class DpapiDataProtector : IDataProtector
{
    private static readonly byte[] Entropy =
        "AppName-Quarantine-v1"u8.ToArray();

    public byte[] Protect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI is only available on Windows");
        return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI is only available on Windows");
        return ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
    }
}
