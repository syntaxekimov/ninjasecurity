using AppName.Service.Ipc;
using System.Text.Json;

namespace AppName.Service.Tests.Ipc;

public class IpcMessagesTests
{
    [Fact]
    public void IpcRequest_SerializesAndDeserializes()
    {
        var request = new IpcRequest("StartScan", JsonSerializer.SerializeToElement(
            new StartScanPayload("Quick", null)));

        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<IpcRequest>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("StartScan", deserialized.Command);
    }

    [Fact]
    public void IpcResponse_Success_SerializesCorrectly()
    {
        var response = IpcResponse.Ok(JsonSerializer.SerializeToElement(new { status = "scanning" }));
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<IpcResponse>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.Success);
        Assert.Null(deserialized.Error);
    }

    [Fact]
    public void IpcResponse_Error_SerializesCorrectly()
    {
        var response = IpcResponse.Fail("Service not ready");
        var json = JsonSerializer.Serialize(response);
        var deserialized = JsonSerializer.Deserialize<IpcResponse>(json);

        Assert.NotNull(deserialized);
        Assert.False(deserialized.Success);
        Assert.Equal("Service not ready", deserialized.Error);
    }
}
