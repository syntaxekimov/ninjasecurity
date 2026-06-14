namespace NinjaSecurity.Service.Tests.Engine;

public class RansomwareDetectorTests
{
    [Fact]
    public void SingleRename_NotDetected()
    {
        var detector = new NinjaSecurity.Service.Engine.RansomwareDetector(threshold: 10, windowSeconds: 5);
        detector.RecordRename("file.doc", "file.doc.locked");
        Assert.False(detector.IsAlarmTriggered);
    }

    [Fact]
    public void MassRenameWithExtensionChange_TriggersAlarm()
    {
        var detector = new NinjaSecurity.Service.Engine.RansomwareDetector(threshold: 5, windowSeconds: 60);
        for (int i = 0; i < 6; i++)
            detector.RecordRename($"file{i}.doc", $"file{i}.doc.locked");
        Assert.True(detector.IsAlarmTriggered);
    }

    [Fact]
    public void RenameWithoutExtensionChange_NotCounted()
    {
        var detector = new NinjaSecurity.Service.Engine.RansomwareDetector(threshold: 5, windowSeconds: 60);
        for (int i = 0; i < 10; i++)
            detector.RecordRename($"file{i}.doc", $"renamed{i}.doc");
        Assert.False(detector.IsAlarmTriggered);
    }

    [Fact]
    public void AlarmRaisedEvent_FiredOnThreshold()
    {
        var detector = new NinjaSecurity.Service.Engine.RansomwareDetector(threshold: 3, windowSeconds: 60);
        bool eventFired = false;
        detector.AlarmRaised += (_, _) => eventFired = true;

        for (int i = 0; i < 3; i++)
            detector.RecordRename($"f{i}.doc", $"f{i}.enc");

        Assert.True(eventFired);
    }

    [Fact]
    public void Reset_ClearsAlarm()
    {
        var detector = new NinjaSecurity.Service.Engine.RansomwareDetector(threshold: 2, windowSeconds: 60);
        detector.RecordRename("a.doc", "a.enc");
        detector.RecordRename("b.doc", "b.enc");
        Assert.True(detector.IsAlarmTriggered);

        detector.Reset();
        Assert.False(detector.IsAlarmTriggered);
    }
}
