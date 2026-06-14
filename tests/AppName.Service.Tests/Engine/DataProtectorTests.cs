using NinjaSecurity.Service.Engine;

namespace NinjaSecurity.Service.Tests.Engine;

public class DataProtectorTests
{
    [Fact]
    public void Passthrough_RoundTrip_ReturnsSameData()
    {
        var protector = new PassthroughDataProtector();
        var original = new byte[] { 1, 2, 3, 4, 5 };

        var protected_ = protector.Protect(original);
        var unprotected = protector.Unprotect(protected_);

        Assert.Equal(original, unprotected);
    }
}
