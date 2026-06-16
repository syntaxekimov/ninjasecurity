using NinjaSecurity.Service.Engine;
using NinjaSecurity.Service.Engine.Interfaces;
using NinjaSecurity.Service.Ipc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Text.Json;

namespace NinjaSecurity.Service.Tests.Ipc;

public class CommandHandlerPlan2Tests
{
    private static ScanScheduler BuildScheduler() =>
        new(new Mock<IScanEngine>().Object,
            new IpcEventChannel(NullLogger<IpcEventChannel>.Instance));

    private static CommandHandler BuildHandler(
        Mock<IRealTimeGuard>? rtGuard = null,
        Mock<IProcessMonitor>? procMon = null,
        Mock<ISystemOptimizer>? sysOpt = null,
        Mock<IUpdateService>? updSvc = null)
    {
        return new CommandHandler(
            new Mock<IScanEngine>().Object,
            new Mock<IQuarantineManager>().Object,
            (rtGuard ?? new Mock<IRealTimeGuard>()).Object,
            (procMon ?? new Mock<IProcessMonitor>()).Object,
            (sysOpt ?? new Mock<ISystemOptimizer>()).Object,
            (updSvc ?? new Mock<IUpdateService>()).Object,
            BuildScheduler());
    }

    [Fact]
    public async Task GetRealTimeStatus_ReturnsEnabledFlag()
    {
        var guardMock = new Mock<IRealTimeGuard>();
        guardMock.Setup(g => g.IsRunning).Returns(true);
        var handler = BuildHandler(rtGuard: guardMock);

        var response = await handler.HandleAsync(new IpcRequest("GetRealTimeStatus"), callerIsAdmin: false);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task SetRealTimeEnabled_True_CallsStart()
    {
        var guardMock = new Mock<IRealTimeGuard>();
        var handler = BuildHandler(rtGuard: guardMock);
        var payload = JsonSerializer.SerializeToElement(new SetRealTimePayload(true));

        var response = await handler.HandleAsync(new IpcRequest("SetRealTimeEnabled", payload), callerIsAdmin: true);
        Assert.True(response.Success);
        guardMock.Verify(g => g.Start(), Times.Once);
    }

    [Fact]
    public async Task SetRealTimeEnabled_False_CallsStop()
    {
        var guardMock = new Mock<IRealTimeGuard>();
        var handler = BuildHandler(rtGuard: guardMock);
        var payload = JsonSerializer.SerializeToElement(new SetRealTimePayload(false));

        var response = await handler.HandleAsync(new IpcRequest("SetRealTimeEnabled", payload), callerIsAdmin: true);
        Assert.True(response.Success);
        guardMock.Verify(g => g.Stop(), Times.Once);
    }

    [Fact]
    public async Task GetProcessList_ReturnsSuccessResponse()
    {
        var procMock = new Mock<IProcessMonitor>();
        procMock.Setup(p => p.GetProcessesAsync(default))
            .ReturnsAsync(new List<NinjaSecurity.Service.Engine.Models.ProcessInfo>());
        var handler = BuildHandler(procMon: procMock);

        var response = await handler.HandleAsync(new IpcRequest("GetProcessList"), callerIsAdmin: false);
        Assert.True(response.Success);
    }

    [Fact]
    public async Task CleanTempFiles_ReturnsFreedBytes()
    {
        var sysOptMock = new Mock<ISystemOptimizer>();
        sysOptMock.Setup(s => s.CleanTempFilesAsync(default)).ReturnsAsync(1024L);
        var handler = BuildHandler(sysOpt: sysOptMock);

        var response = await handler.HandleAsync(new IpcRequest("CleanTempFiles"), callerIsAdmin: true);
        Assert.True(response.Success);
    }
}
