using NinjaSecurity.Service.Engine;

namespace NinjaSecurity.Service.Tests.Engine;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckUpdates_ReturnsUpdateInfo_WithLastChecked()
    {
        var service = new UpdateService(httpClient: null, yaraRulesPath: "/nonexistent");
        var info = await service.CheckUpdatesAsync();

        Assert.NotNull(info);
        Assert.NotNull(info.LastChecked);
    }

    [Fact]
    public async Task UpdateYaraRules_NonExistentPath_ReturnsFalse()
    {
        var service = new UpdateService(httpClient: null, yaraRulesPath: "/nonexistent/path");
        var result = await service.UpdateYaraRulesAsync();
        Assert.False(result);
    }

    [Fact]
    public void LastUpdated_InitiallyNull()
    {
        var service = new UpdateService(httpClient: null, yaraRulesPath: "/nonexistent");
        Assert.Null(service.LastUpdated);
    }

    [Fact]
    public async Task UpdateMalSearcherDb_WithNullHttpClient_ReturnsFalse()
    {
        var service = new UpdateService(httpClient: null, yaraRulesPath: "/nonexistent");
        var result = await service.UpdateMalSearcherDbAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateSignatures_WithNullHttpClient_ReturnsFalse()
    {
        var service = new UpdateService(httpClient: null, yaraRulesPath: "/nonexistent");
        var result = await service.UpdateSignaturesAsync();
        Assert.False(result);
    }
}
