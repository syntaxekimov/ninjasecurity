using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace NinjaSecurity.Service.Ipc;

internal static class IpcPipeSecurity
{
    [SupportedOSPlatform("windows")]
    internal static PipeSecurity CreateAdminOnlySecurity()
    {
        var security = new PipeSecurity();
        var admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var system  = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        security.AddAccessRule(new PipeAccessRule(admins, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(system,  PipeAccessRights.FullControl, AccessControlType.Allow));
        return security;
    }
}
