using NinjaSecurity.Service.Engine.Interfaces;

namespace NinjaSecurity.Service.Engine;

// For testing only — does NOT encrypt the key. Never use in production.
public class PassthroughDataProtector : IDataProtector
{
    public byte[] Protect(byte[] data) => data;
    public byte[] Unprotect(byte[] data) => data;
}
