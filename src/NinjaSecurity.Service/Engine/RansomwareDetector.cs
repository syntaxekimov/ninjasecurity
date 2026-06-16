namespace NinjaSecurity.Service.Engine;

public class RansomwareDetector
{
    private readonly int _threshold;
    private readonly TimeSpan _window;
    private readonly Queue<DateTime> _events = new();
    private bool _alarmTriggered;

    public bool IsAlarmTriggered => _alarmTriggered;
    public event EventHandler? AlarmRaised;

    public RansomwareDetector(int threshold = 50, int windowSeconds = 5)
    {
        _threshold = threshold;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public void RecordRename(string oldName, string newName)
    {
        if (_alarmTriggered) return;

        var oldExt = Path.GetExtension(oldName).ToLowerInvariant();
        var newExt = Path.GetExtension(newName).ToLowerInvariant();
        if (oldExt == newExt) return;

        var now = DateTime.UtcNow;
        _events.Enqueue(now);

        while (_events.Count > 0 && now - _events.Peek() > _window)
            _events.Dequeue();

        if (_events.Count >= _threshold)
        {
            _alarmTriggered = true;
            AlarmRaised?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Reset()
    {
        _events.Clear();
        _alarmTriggered = false;
    }
}
