using NinjaSecurity.Service;

namespace NinjaSecurity.Service.Tests;

public class AppPathsTests
{
    [Fact]
    public void DatabasePath_EndsWithDbFile()
    {
        Assert.EndsWith("ninjasecurity.db", AppPaths.DatabasePath);
    }

    [Fact]
    public void QuarantinePath_IsUnderAppData()
    {
        Assert.Contains("NinjaSecurity", AppPaths.QuarantinePath);
        Assert.EndsWith("Quarantine", AppPaths.QuarantinePath);
    }
}
