using NinjaSecurity.Service.Engine;

namespace NinjaSecurity.Service.Tests.Engine;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckUpdates_ReturnsUpdateInfo_WithLastChecked()
    {
        var service = new UpdateService(httpClient: null);
        var info = await service.CheckUpdatesAsync();

        Assert.NotNull(info);
        Assert.NotNull(info.LastChecked);
    }

    [Fact]
    public async Task UpdateYaraRules_WithNullHttpClient_ReturnsFalse()
    {
        var service = new UpdateService(httpClient: null);
        var result = await service.UpdateYaraRulesAsync();
        Assert.False(result);
    }

    [Fact]
    public void LastUpdated_InitiallyNull()
    {
        var service = new UpdateService(httpClient: null);
        Assert.Null(service.LastUpdated);
    }

    [Fact]
    public async Task UpdateMalSearcherDb_WithNullHttpClient_ReturnsFalse()
    {
        var service = new UpdateService(httpClient: null);
        var result = await service.UpdateMalSearcherDbAsync();
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateSignatures_WithNullClamAvDaemon_ReturnsFalse()
    {
        var service = new UpdateService(httpClient: null, clamAvDaemon: null);
        var result = await service.UpdateSignaturesAsync();
        Assert.False(result);
    }
}
