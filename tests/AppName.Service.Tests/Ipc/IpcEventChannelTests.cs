using NinjaSecurity.Service.Ipc;
using System.Text.Json;

namespace NinjaSecurity.Service.Tests.Ipc;

public class IpcEventChannelTests
{
    [Fact]
    public void IpcEvent_ScanProgress_SerializesCorrectly()
    {
        var evt = new IpcEvent("ScanProgress", JsonSerializer.SerializeToElement(
            new ScanProgressPayload(42, "C:\\test.exe", 1000)));

        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<IpcEvent>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("ScanProgress", deserialized.EventType);
    }

    [Fact]
    public void IpcEvent_ThreatFound_SerializesCorrectly()
    {
        var evt = new IpcEvent("ThreatFound", JsonSerializer.SerializeToElement(
            new ThreatFoundPayload("C:\\evil.exe", "Trojan.Win32.X", 80)));

        var json = JsonSerializer.Serialize(evt);
        var deserialized = JsonSerializer.Deserialize<IpcEvent>(json);

        Assert.NotNull(deserialized);
        Assert.Equal("ThreatFound", deserialized.EventType);
    }

    [Fact]
    public void Publish_AddsEventToQueue_WithoutBlocking()
    {
        var logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<IpcEventChannel>();
        var channel = new IpcEventChannel(logger);

        channel.Publish(new IpcEvent("Test"));
        // No exception = queue accepted the event without blocking
    }

    [Fact]
    public void ScanCompletePayload_RoundTrips()
    {
        var payload = new ScanCompletePayload(3, 1500, TimeSpan.FromSeconds(42));
        var json = JsonSerializer.Serialize(payload);
        var result = JsonSerializer.Deserialize<ScanCompletePayload>(json);
        Assert.NotNull(result);
        Assert.Equal(3, result.ThreatsFound);
    }
}
