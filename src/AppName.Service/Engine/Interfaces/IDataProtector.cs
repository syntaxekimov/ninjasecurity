namespace AppName.Service.Engine.Interfaces;

public interface IDataProtector
{
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] data);
}
