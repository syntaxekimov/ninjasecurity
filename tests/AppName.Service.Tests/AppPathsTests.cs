using AppName.Service;

namespace AppName.Service.Tests;

public class AppPathsTests
{
    [Fact]
    public void DatabasePath_EndsWithDbFile()
    {
        Assert.EndsWith("appname.db", AppPaths.DatabasePath);
    }

    [Fact]
    public void QuarantinePath_IsUnderAppData()
    {
        Assert.Contains("AppName", AppPaths.QuarantinePath);
        Assert.EndsWith("Quarantine", AppPaths.QuarantinePath);
    }
}
