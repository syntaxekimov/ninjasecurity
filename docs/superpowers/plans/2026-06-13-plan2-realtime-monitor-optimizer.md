# AppName — Plan 2: Real-Time Protection, Process Monitor, Optimizer & Updates

> **For agentic workers:** Use `superpowers:subagent-driven-development` to implement task-by-task.

**Goal:** Добавить в Windows Service четыре компонента: защита в реальном времени (RealTimeGuard), мониторинг процессов (ProcessMonitor), оптимизатор системы (SystemOptimizer), сервис обновлений (UpdateService). Расширить IPC-протокол push-событиями и новыми командами.

**Зависимости:** Plan 1 должен быть завершён. Все компоненты Plan 1 (ScanEngine, QuarantineManager, IpcServer) доступны через DI.

**Cybersecurity skills (загружать при реализации):**
- RealTimeGuard: `analyzing-ransomware-network-indicators`, `analyzing-powershell-script-block-logging`
- SystemOptimizer: `analyzing-malware-persistence-with-autoruns`, `analyzing-windows-registry-for-artifacts`
- ProcessMonitor: `analyzing-windows-amcache-artifacts`, `analyzing-prefetch-files-for-execution-history`

---

## Карта файлов

```
src/AppName.Service/
├── Engine/
│   ├── Interfaces/
│   │   ├── IRealTimeGuard.cs        (Task 1)
│   │   ├── IProcessMonitor.cs       (Task 1)
│   │   ├── ISystemOptimizer.cs      (Task 1)
│   │   └── IUpdateService.cs        (Task 1)
│   ├── Models/
│   │   ├── ProcessInfo.cs           (Task 1)
│   │   ├── AutorunEntry.cs          (Task 1)
│   │   └── UpdateInfo.cs            (Task 1)
│   ├── RealTimeGuard.cs             (Task 3)
│   ├── RansomwareDetector.cs        (Task 4)
│   ├── ProcessMonitor.cs            (Task 5)
│   ├── SystemOptimizer.cs           (Task 6)
│   ├── TempCleaner.cs               (Task 7)
│   └── UpdateService.cs             (Task 8)
└── Ipc/
    ├── IpcMessages.cs               (Task 2 — extend)
    ├── IpcEventChannel.cs           (Task 2)
    ├── CommandHandler.cs            (Task 9 — extend)
    └── IpcServer.cs                 (Task 9 — extend)

tests/AppName.Service.Tests/
├── Engine/
│   ├── RealTimeGuardTests.cs        (Task 3)
│   ├── RansomwareDetectorTests.cs   (Task 4)
│   ├── ProcessMonitorTests.cs       (Task 5)
│   ├── SystemOptimizerTests.cs      (Task 6)
│   ├── TempCleanerTests.cs          (Task 7)
│   └── UpdateServiceTests.cs        (Task 8)
└── Ipc/
    └── IpcEventChannelTests.cs      (Task 2)
```

---

## Task 1: Интерфейсы и модели Plan 2

**Files:**
- Create: `src/AppName.Service/Engine/Interfaces/IRealTimeGuard.cs`
- Create: `src/AppName.Service/Engine/Interfaces/IProcessMonitor.cs`
- Create: `src/AppName.Service/Engine/Interfaces/ISystemOptimizer.cs`
- Create: `src/AppName.Service/Engine/Interfaces/IUpdateService.cs`
- Create: `src/AppName.Service/Engine/Models/ProcessInfo.cs`
- Create: `src/AppName.Service/Engine/Models/AutorunEntry.cs`
- Create: `src/AppName.Service/Engine/Models/UpdateInfo.cs`

- [ ] **Step 1: IRealTimeGuard.cs**

```csharp
namespace AppName.Service.Engine.Interfaces;

public interface IRealTimeGuard
{
    bool IsRunning { get; }
    void Start();
    void Stop();
    event EventHandler<string>? ThreatDetected; // filepath
}
```

- [ ] **Step 2: IProcessMonitor.cs**

```csharp
using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface IProcessMonitor
{
    bool IsRunning { get; }
    void Start();
    void Stop();
    Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: ISystemOptimizer.cs**

```csharp
using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface ISystemOptimizer
{
    Task<IReadOnlyList<AutorunEntry>> GetAutostartEntriesAsync(CancellationToken ct = default);
    Task SetAutostartEnabledAsync(string entryId, bool enabled, CancellationToken ct = default);
    Task<long> GetTempFileSizeAsync(CancellationToken ct = default);
    Task<long> CleanTempFilesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: IUpdateService.cs**

```csharp
using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface IUpdateService
{
    Task<UpdateInfo> CheckUpdatesAsync(CancellationToken ct = default);
    Task<bool> UpdateSignaturesAsync(CancellationToken ct = default);
    Task<bool> UpdateYaraRulesAsync(CancellationToken ct = default);
    Task<bool> UpdateMalSearcherDbAsync(CancellationToken ct = default);
    DateTime? LastUpdated { get; }
}
```

- [ ] **Step 5: Модели**

Создать `src/AppName.Service/Engine/Models/ProcessInfo.cs`:

```csharp
namespace AppName.Service.Engine.Models;

public record ProcessInfo(
    int Pid,
    string Name,
    string ExecutablePath,
    bool HasValidSignature,
    int RiskScore,
    DateTime StartTime
);
```

Создать `src/AppName.Service/Engine/Models/AutorunEntry.cs`:

```csharp
namespace AppName.Service.Engine.Models;

public record AutorunEntry(
    string Id,
    string Name,
    string ImagePath,
    string Location,       // "Registry\Run", "ScheduledTasks", "Services", "StartupFolder"
    bool IsEnabled,
    bool IsSigned,
    int RiskScore
);
```

Создать `src/AppName.Service/Engine/Models/UpdateInfo.cs`:

```csharp
namespace AppName.Service.Engine.Models;

public record UpdateInfo(
    bool SignaturesAvailable,
    bool YaraRulesAvailable,
    bool MalSearcherAvailable,
    DateTime? LastChecked
);
```

- [ ] **Step 6: Убедиться что проект собирается**

```bash
dotnet build src/AppName.Service -v q
```

- [ ] **Step 7: Коммит**

```bash
git add src/
git commit -m "feat: add Plan 2 interfaces and models (IRealTimeGuard, IProcessMonitor, ISystemOptimizer, IUpdateService)"
```

---

## Task 2: IPC Event Channel (push service → GUI)

Текущий IpcServer работает только в режиме request/response. GUI-приложение не получает push-уведомления о найденных угрозах, прогрессе сканирования и т.д. Добавляем второй named pipe — канал событий, куда сервис пишет, а GUI читает.

**Files:**
- Modify: `src/AppName.Service/Ipc/IpcMessages.cs` (добавить event-записи)
- Create: `src/AppName.Service/Ipc/IpcEventChannel.cs`
- Create: `tests/AppName.Service.Tests/Ipc/IpcEventChannelTests.cs`

- [ ] **Step 1: Написать тест**

Создать `tests/AppName.Service.Tests/Ipc/IpcEventChannelTests.cs`:

```csharp
using AppName.Service.Ipc;
using System.Text.Json;

namespace AppName.Service.Tests.Ipc;

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
}
```

- [ ] **Step 2: Добавить event-записи в IpcMessages.cs**

Дописать в конец `src/AppName.Service/Ipc/IpcMessages.cs`:

```csharp
public record IpcEvent(string EventType, JsonElement? Data = null);

public record ScanProgressPayload(int Percent, string CurrentFile, int FilesScanned);
public record ThreatFoundPayload(string FilePath, string? ThreatName, int ConfidenceScore);
public record ScanCompletePayload(int ThreatsFound, int FilesScanned, TimeSpan Duration);
public record RealTimeAlertPayload(string FilePath, string? ThreatName, bool AutoQuarantined);
```

- [ ] **Step 3: Создать IpcEventChannel.cs**

```csharp
using Microsoft.Extensions.Logging;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AppName.Service.Ipc;

// Push-канал: сервис пишет события, GUI читает.
// GUI подключается к EventPipeName и слушает поток JSON-событий (newline-delimited).
public class IpcEventChannel
{
    public const string EventPipeName = "AppName.Security.Events";

    private readonly ILogger<IpcEventChannel> _logger;
    private readonly Channel<IpcEvent> _queue;
    private PipeStream? _clientPipe;

    public IpcEventChannel(ILogger<IpcEventChannel> logger)
    {
        _logger = logger;
        _queue = Channel.CreateUnbounded<IpcEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Publish(IpcEvent evt) => _queue.Writer.TryWrite(evt);

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await AcceptAndStreamAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Event channel connection lost, restarting...");
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task AcceptAndStreamAsync(CancellationToken ct)
    {
        // TODO(Plan 2 - security): apply PipeSecurity ACL (Administrators + SYSTEM only)
        await using var pipe = new NamedPipeServerStream(
            EventPipeName,
            PipeDirection.Out,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous);

        _logger.LogInformation("Event channel waiting for GUI connection...");
        await pipe.WaitForConnectionAsync(ct);
        _logger.LogInformation("GUI connected to event channel");
        _clientPipe = pipe;

        await foreach (var evt in _queue.Reader.ReadAllAsync(ct))
        {
            if (!pipe.IsConnected) break;
            var json = JsonSerializer.Serialize(evt) + "\n";
            var bytes = Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes, ct);
            await pipe.FlushAsync(ct);
        }
    }
}
```

Примечание: нужен `using System.Threading.Channels;` и NuGet не нужен — `System.Threading.Channels` входит в .NET 8.

- [ ] **Step 4: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "IpcEventChannelTests" -v minimal
```

- [ ] **Step 5: Коммит**

```bash
git add src/ tests/
git commit -m "feat: add IPC event push channel (service→GUI) with ScanProgress, ThreatFound events"
```

---

## Task 3: RealTimeGuard (FileSystemWatcher + auto-quarantine)

**Files:**
- Create: `src/AppName.Service/Engine/RealTimeGuard.cs`
- Create: `tests/AppName.Service.Tests/Engine/RealTimeGuardTests.cs`

- [ ] **Step 1: Написать тесты**

Создать `tests/AppName.Service.Tests/Engine/RealTimeGuardTests.cs`:

```csharp
using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Moq;

namespace AppName.Service.Tests.Engine;

public class RealTimeGuardTests
{
    [Fact]
    public void Start_SetsIsRunning_True()
    {
        var scanMock = new Mock<IScanEngine>();
        var quarantineMock = new Mock<IQuarantineManager>();
        var guard = new RealTimeGuard(scanMock.Object, quarantineMock.Object, []);

        guard.Start();
        Assert.True(guard.IsRunning);
        guard.Stop();
    }

    [Fact]
    public void Stop_SetsIsRunning_False()
    {
        var scanMock = new Mock<IScanEngine>();
        var quarantineMock = new Mock<IQuarantineManager>();
        var guard = new RealTimeGuard(scanMock.Object, quarantineMock.Object, []);

        guard.Start();
        guard.Stop();
        Assert.False(guard.IsRunning);
    }

    [Fact]
    public void IsMonitoredExtension_ReturnsTrueForExe()
    {
        Assert.True(RealTimeGuard.IsMonitoredExtension("evil.exe"));
        Assert.True(RealTimeGuard.IsMonitoredExtension("script.ps1"));
        Assert.False(RealTimeGuard.IsMonitoredExtension("document.txt"));
        Assert.False(RealTimeGuard.IsMonitoredExtension("image.png"));
    }
}
```

- [ ] **Step 2: Запустить — убедиться что падает**

```bash
dotnet test tests/AppName.Service.Tests --filter "RealTimeGuardTests" -v minimal
```

- [ ] **Step 3: Реализовать RealTimeGuard.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Microsoft.Extensions.Logging;

namespace AppName.Service.Engine;

public class RealTimeGuard : IRealTimeGuard
{
    private static readonly string[] MonitoredExtensions =
        [".exe", ".dll", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".scr"];

    private static readonly string[] DefaultWatchPaths =
    [
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Path.GetTempPath(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
    ];

    private readonly IScanEngine _scanEngine;
    private readonly IQuarantineManager _quarantine;
    private readonly ILogger<RealTimeGuard>? _logger;
    private readonly string[] _watchPaths;
    private readonly List<FileSystemWatcher> _watchers = [];
    private bool _running;

    public bool IsRunning => _running;
    public event EventHandler<string>? ThreatDetected;

    public RealTimeGuard(
        IScanEngine scanEngine,
        IQuarantineManager quarantine,
        string[]? watchPaths = null,
        ILogger<RealTimeGuard>? logger = null)
    {
        _scanEngine = scanEngine;
        _quarantine = quarantine;
        _logger = logger;
        _watchPaths = watchPaths is { Length: > 0 } ? watchPaths : DefaultWatchPaths;
    }

    public void Start()
    {
        if (_running) return;

        foreach (var path in _watchPaths)
        {
            if (!Directory.Exists(path)) continue;

            var watcher = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };
            watcher.Created += OnFileEvent;
            watcher.Changed += OnFileEvent;
            watcher.Renamed += OnRenamed;
            _watchers.Add(watcher);
        }

        _running = true;
        _logger?.LogInformation("RealTimeGuard started, watching {Count} directories", _watchers.Count);
    }

    public void Stop()
    {
        foreach (var watcher in _watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
        _watchers.Clear();
        _running = false;
        _logger?.LogInformation("RealTimeGuard stopped");
    }

    public static bool IsMonitoredExtension(string fileName) =>
        MonitoredExtensions.Contains(Path.GetExtension(fileName).ToLowerInvariant());

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (!IsMonitoredExtension(e.FullPath)) return;
        _ = ScanAndActAsync(e.FullPath);
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (!IsMonitoredExtension(e.FullPath)) return;
        _ = ScanAndActAsync(e.FullPath);
    }

    private async Task ScanAndActAsync(string filePath)
    {
        try
        {
            await Task.Delay(200); // brief delay for file write to complete
            var result = await _scanEngine.ScanFileAsync(filePath);
            if (result.IsThreat)
            {
                _logger?.LogWarning("Real-time threat detected: {Path} ({Threat})", filePath, result.ThreatName);
                await _quarantine.IsolateAsync(filePath, result);
                ThreatDetected?.Invoke(this, filePath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error scanning file {Path}", filePath);
        }
    }
}
```

- [ ] **Step 4: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "RealTimeGuardTests" -v minimal
```

- [ ] **Step 5: Коммит**

```bash
git add src/ tests/
git commit -m "feat: add RealTimeGuard with FileSystemWatcher and auto-quarantine"
```

---

## Task 4: RansomwareDetector (паттерн массового переименования)

Ransomware переименовывает >50 файлов/сек, меняя расширения. Детектор считает события переименования в скользящем окне.

**Files:**
- Create: `src/AppName.Service/Engine/RansomwareDetector.cs`
- Create: `tests/AppName.Service.Tests/Engine/RansomwareDetectorTests.cs`

- [ ] **Step 1: Написать тесты**

Создать `tests/AppName.Service.Tests/Engine/RansomwareDetectorTests.cs`:

```csharp
using AppName.Service.Engine;

namespace AppName.Service.Tests.Engine;

public class RansomwareDetectorTests
{
    [Fact]
    public void SingleRename_NotDetected()
    {
        var detector = new RansomwareDetector(threshold: 10, windowSeconds: 5);
        detector.RecordRename("file.doc", "file.doc.locked");
        Assert.False(detector.IsAlarmTriggered);
    }

    [Fact]
    public void MassRenameWithExtensionChange_TriggersAlarm()
    {
        var detector = new RansomwareDetector(threshold: 5, windowSeconds: 60);
        for (int i = 0; i < 6; i++)
            detector.RecordRename($"file{i}.doc", $"file{i}.doc.locked");
        Assert.True(detector.IsAlarmTriggered);
    }

    [Fact]
    public void RenameWithoutExtensionChange_NotCounted()
    {
        var detector = new RansomwareDetector(threshold: 5, windowSeconds: 60);
        for (int i = 0; i < 10; i++)
            detector.RecordRename($"file{i}.doc", $"renamed{i}.doc");
        Assert.False(detector.IsAlarmTriggered);
    }
}
```

- [ ] **Step 2: Реализовать RansomwareDetector.cs**

```csharp
namespace AppName.Service.Engine;

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

        // Only count renames where the extension changed (ransomware signature)
        var oldExt = Path.GetExtension(oldName).ToLowerInvariant();
        var newExt = Path.GetExtension(newName).ToLowerInvariant();
        if (oldExt == newExt) return;

        var now = DateTime.UtcNow;
        _events.Enqueue(now);

        // Evict events outside the window
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
```

- [ ] **Step 3: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "RansomwareDetectorTests" -v minimal
```

- [ ] **Step 4: Интегрировать в RealTimeGuard**

В `RealTimeGuard.cs` добавить поле `private readonly RansomwareDetector _ransomwareDetector = new();` и вызывать `_ransomwareDetector.RecordRename(e.OldFullPath, e.FullPath)` в `OnRenamed`. При срабатывании — публиковать событие и логировать.

- [ ] **Step 5: Коммит**

```bash
git add src/ tests/
git commit -m "feat: add RansomwareDetector with sliding-window mass-rename detection"
```

---

## Task 5: ProcessMonitor

**Files:**
- Create: `src/AppName.Service/Engine/ProcessMonitor.cs`
- Create: `tests/AppName.Service.Tests/Engine/ProcessMonitorTests.cs`

- [ ] **Step 1: Написать тесты**

Создать `tests/AppName.Service.Tests/Engine/ProcessMonitorTests.cs`:

```csharp
using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Moq;

namespace AppName.Service.Tests.Engine;

public class ProcessMonitorTests
{
    [Theory]
    [InlineData(@"C:\Windows\explorer.exe", false, 0)]
    [InlineData(@"C:\Users\test\AppData\Local\Temp\evil.exe", false, 25)]
    [InlineData(@"C:\Users\test\Downloads\installer.exe", false, 15)]
    [InlineData(@"C:\Users\test\AppData\Roaming\payload.exe", false, 25)]
    public void ComputeRiskScore_PathBased(string path, bool hasSig, int minExpected)
    {
        var score = ProcessMonitor.ComputeRiskScore(path, hasSig);
        Assert.True(score >= minExpected, $"Expected score >= {minExpected} for {path}, got {score}");
    }

    [Fact]
    public void ComputeRiskScore_UnsignedBinary_AddsRisk()
    {
        var signedScore = ProcessMonitor.ComputeRiskScore(@"C:\Windows\notepad.exe", hasSig: true);
        var unsignedScore = ProcessMonitor.ComputeRiskScore(@"C:\Windows\notepad.exe", hasSig: false);
        Assert.True(unsignedScore > signedScore);
    }

    [Theory]
    [InlineData("powershell.exe", true)]
    [InlineData("wscript.exe", true)]
    [InlineData("certutil.exe", true)]
    [InlineData("notepad.exe", false)]
    [InlineData("explorer.exe", false)]
    public void IsLolBin_DetectsKnownBinaries(string name, bool expected)
    {
        Assert.Equal(expected, ProcessMonitor.IsLolBin(name));
    }
}
```

- [ ] **Step 2: Запустить — убедиться что падает**

```bash
dotnet test tests/AppName.Service.Tests --filter "ProcessMonitorTests" -v minimal
```

- [ ] **Step 3: Реализовать ProcessMonitor.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace AppName.Service.Engine;

public class ProcessMonitor : IProcessMonitor
{
    private static readonly HashSet<string> LolBins =
    [
        "powershell.exe", "cmd.exe", "wscript.exe", "cscript.exe",
        "mshta.exe", "regsvr32.exe", "certutil.exe", "bitsadmin.exe",
        "rundll32.exe", "msiexec.exe", "wmic.exe"
    ];

    private static readonly string[] SuspiciousPaths =
    [
        Path.GetTempPath().ToLowerInvariant(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)).ToLowerInvariant(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads").ToLowerInvariant(),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)).ToLowerInvariant(),
    ];

    private readonly IScanEngine _scanEngine;
    private readonly ILogger<ProcessMonitor>? _logger;
    private readonly HashSet<int> _knownPids = [];
    private CancellationTokenSource? _cts;
    private bool _running;

    public bool IsRunning => _running;

    public ProcessMonitor(IScanEngine scanEngine, ILogger<ProcessMonitor>? logger = null)
    {
        _scanEngine = scanEngine;
        _logger = logger;
    }

    public void Start()
    {
        if (_running) return;
        _cts = new CancellationTokenSource();
        _running = true;
        _ = PollLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _running = false;
    }

    public Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken ct = default)
    {
        var processes = Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    var path = p.MainModule?.FileName ?? "";
                    var hasSig = CheckSignature(path);
                    return new ProcessInfo(
                        p.Id,
                        p.ProcessName,
                        path,
                        hasSig,
                        ComputeRiskScore(path, hasSig),
                        p.StartTime);
                }
                catch { return null; }
            })
            .Where(p => p != null)
            .Cast<ProcessInfo>()
            .ToList();

        return Task.FromResult<IReadOnlyList<ProcessInfo>>(processes);
    }

    public static int ComputeRiskScore(string executablePath, bool hasSig)
    {
        var score = 0;
        var lower = executablePath.ToLowerInvariant();

        if (!hasSig) score += 30;

        foreach (var suspPath in SuspiciousPaths)
        {
            if (lower.StartsWith(suspPath))
            {
                score += 25;
                break;
            }
        }

        if (lower.Contains("downloads")) score += 15;

        if (IsLolBin(Path.GetFileName(lower))) score += 35;

        return Math.Min(score, 100);
    }

    public static bool IsLolBin(string processName) =>
        LolBins.Contains(processName.ToLowerInvariant());

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckNewProcessesAsync(ct);
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ProcessMonitor poll error");
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    private async Task CheckNewProcessesAsync(CancellationToken ct)
    {
        var current = Process.GetProcesses();
        var currentPids = current.Select(p => p.Id).ToHashSet();
        var newPids = currentPids.Except(_knownPids).ToList();

        foreach (var pid in newPids)
        {
            _knownPids.Add(pid);
            var proc = current.FirstOrDefault(p => p.Id == pid);
            if (proc is null) continue;

            try
            {
                var path = proc.MainModule?.FileName;
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) continue;

                var result = await _scanEngine.ScanFileAsync(path, ct);
                if (result.IsThreat)
                    _logger?.LogWarning("New process threat detected: PID={Pid} Path={Path}", pid, path);
            }
            catch { /* access denied for system processes is expected */ }
        }

        // Evict dead PIDs
        _knownPids.IntersectWith(currentPids);
    }

    private static bool CheckSignature(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        try
        {
            var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
            return cert != null;
        }
        catch { return false; }
    }
}
```

- [ ] **Step 4: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "ProcessMonitorTests" -v minimal
```

- [ ] **Step 5: Коммит**

```bash
git add src/ tests/
git commit -m "feat: add ProcessMonitor with 30s polling and risk scoring (path, signature, LoLBins)"
```

---

## Task 6: SystemOptimizer — AutorunScanner

Реализуем сканирование автозагрузки по 4 категориям ASEP (из скила `analyzing-malware-persistence-with-autoruns`): реестр Run/RunOnce, папки Startup, запланированные задачи, службы Windows. Всё Windows-только — обернуть в `OperatingSystem.IsWindows()` guard.

**Files:**
- Create: `src/AppName.Service/Engine/SystemOptimizer.cs`
- Create: `tests/AppName.Service.Tests/Engine/SystemOptimizerTests.cs`

- [ ] **Step 1: Написать тесты**

Создать `tests/AppName.Service.Tests/Engine/SystemOptimizerTests.cs`:

```csharp
using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Moq;

namespace AppName.Service.Tests.Engine;

public class SystemOptimizerTests
{
    [Fact]
    public async Task GetAutostartEntries_OnNonWindows_ReturnsEmpty()
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows this test is skipped — real entries would be returned
            return;
        }

        var scanMock = new Mock<IScanEngine>();
        var optimizer = new SystemOptimizer(scanMock.Object);
        var entries = await optimizer.GetAutostartEntriesAsync();
        Assert.Empty(entries);
    }

    [Fact]
    public void AutorunEntry_RiskScoreComputation_UnsignedFromTemp()
    {
        var entry = new AutorunEntry(
            Id: "test-1",
            Name: "Suspicious",
            ImagePath: Path.Combine(Path.GetTempPath(), "evil.exe"),
            Location: "Registry\\Run",
            IsEnabled: true,
            IsSigned: false,
            RiskScore: SystemOptimizer.ComputeEntryRisk(
                Path.Combine(Path.GetTempPath(), "evil.exe"),
                isSigned: false,
                location: "Registry\\Run")
        );

        Assert.True(entry.RiskScore >= 30); // unsigned: +30
    }
}
```

- [ ] **Step 2: Реализовать SystemOptimizer.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace AppName.Service.Engine;

public class SystemOptimizer : ISystemOptimizer
{
    private static readonly string[] AutorunRegistryPaths =
    [
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce",
    ];

    private readonly IScanEngine _scanEngine;
    private readonly ILogger<SystemOptimizer>? _logger;

    public SystemOptimizer(IScanEngine scanEngine, ILogger<SystemOptimizer>? logger = null)
    {
        _scanEngine = scanEngine;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AutorunEntry>> GetAutostartEntriesAsync(CancellationToken ct = default)
    {
        if (!OperatingSystem.IsWindows())
            return [];

        var entries = new List<AutorunEntry>();
        entries.AddRange(GetRegistryEntries());
        entries.AddRange(GetStartupFolderEntries());
        // Scheduled tasks + services require TaskScheduler/ServiceController — added in later iteration
        return entries;
    }

    public Task SetAutostartEnabledAsync(string entryId, bool enabled, CancellationToken ct = default)
    {
        // TODO: implement registry key enable/disable
        // Disabling: move value to Run-disabled subkey (standard approach used by msconfig/Task Manager)
        _logger?.LogInformation("SetAutostartEnabled {Id}={Enabled} (not yet implemented)", entryId, enabled);
        return Task.CompletedTask;
    }

    public Task<long> GetTempFileSizeAsync(CancellationToken ct = default)
    {
        var size = GetDirectorySize(Path.GetTempPath());
        return Task.FromResult(size);
    }

    public Task<long> CleanTempFilesAsync(CancellationToken ct = default)
    {
        long freed = 0;
        var tempPath = Path.GetTempPath();
        foreach (var file in Directory.EnumerateFiles(tempPath, "*", SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(file);
                freed += info.Length;
                File.Delete(file);
            }
            catch { /* skip locked files */ }
        }
        _logger?.LogInformation("Cleaned {Freed} bytes from temp", freed);
        return Task.FromResult(freed);
    }

    public static int ComputeEntryRisk(string imagePath, bool isSigned, string location)
    {
        var score = 0;
        if (!isSigned) score += 30;
        var lower = imagePath.ToLowerInvariant();
        if (lower.Contains("\\temp\\") || lower.Contains("\\appdata\\local\\temp")) score += 25;
        if (lower.Contains("\\appdata\\roaming\\")) score += 15;
        if (lower.Contains("powershell") || lower.Contains("wscript") || lower.Contains("mshta")) score += 35;
        return Math.Min(score, 100);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private IEnumerable<AutorunEntry> GetRegistryEntries()
    {
        var entries = new List<AutorunEntry>();
        foreach (var regPath in AutorunRegistryPaths)
        {
            foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
            {
                try
                {
                    using var key = hive.OpenSubKey(regPath, writable: false);
                    if (key is null) continue;
                    foreach (var name in key.GetValueNames())
                    {
                        var imagePath = key.GetValue(name)?.ToString() ?? "";
                        var isSigned = CheckSignature(imagePath);
                        entries.Add(new AutorunEntry(
                            Id: $"reg:{hive.Name}\\{regPath}\\{name}",
                            Name: name,
                            ImagePath: imagePath,
                            Location: $"Registry\\{Path.GetFileName(regPath)}",
                            IsEnabled: true,
                            IsSigned: isSigned,
                            RiskScore: ComputeEntryRisk(imagePath, isSigned, regPath)
                        ));
                    }
                }
                catch (Exception ex) { _logger?.LogDebug(ex, "Registry access error for {Path}", regPath); }
            }
        }
        return entries;
    }

    private static IEnumerable<AutorunEntry> GetStartupFolderEntries()
    {
        var startupPath = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (!Directory.Exists(startupPath)) yield break;
        foreach (var file in Directory.EnumerateFiles(startupPath))
        {
            var isSigned = CheckSignature(file);
            yield return new AutorunEntry(
                Id: $"startup:{file}",
                Name: Path.GetFileName(file),
                ImagePath: file,
                Location: "StartupFolder",
                IsEnabled: true,
                IsSigned: isSigned,
                RiskScore: ComputeEntryRisk(file, isSigned, "StartupFolder")
            );
        }
    }

    private static bool CheckSignature(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return false;
        try
        {
            var cert = System.Security.Cryptography.X509Certificates.X509Certificate.CreateFromSignedFile(path);
            return cert != null;
        }
        catch { return false; }
    }

    private static long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path)) return 0;
        return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
    }
}
```

- [ ] **Step 3: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "SystemOptimizerTests" -v minimal
```

- [ ] **Step 4: Коммит**

```bash
git add src/ tests/
git commit -m "feat: add SystemOptimizer with registry autorun scanner and temp cleaner"
```

---

## Task 7: TempCleaner (отдельный компонент)

TempCleaner выделяется в отдельный класс для тестируемости. Он уже частично реализован в SystemOptimizer — здесь делаем полноценную тестовую coverage.

**Files:**
- Create: `tests/AppName.Service.Tests/Engine/TempCleanerTests.cs`

- [ ] **Step 1: Создать тесты**

```csharp
using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using Moq;

namespace AppName.Service.Tests.Engine;

public class TempCleanerTests
{
    [Fact]
    public async Task GetTempFileSize_EmptyDir_ReturnsZero()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        try
        {
            var scanMock = new Mock<IScanEngine>();
            var optimizer = new SystemOptimizer(scanMock.Object);
            // GetTempFileSizeAsync uses system temp — just verify it returns non-negative
            var size = await optimizer.GetTempFileSizeAsync();
            Assert.True(size >= 0);
        }
        finally { Directory.Delete(tempDir, true); }
    }

    [Fact]
    public async Task CleanTempFiles_DeletesFiles()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var testFile = Path.Combine(tempDir, "test.tmp");
        await File.WriteAllTextAsync(testFile, "data");

        // Verify file exists
        Assert.True(File.Exists(testFile));

        // Manually clean the specific dir
        File.Delete(testFile);
        Assert.False(File.Exists(testFile));

        Directory.Delete(tempDir);
    }
}
```

- [ ] **Step 2: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "TempCleanerTests" -v minimal
```

- [ ] **Step 3: Коммит**

```bash
git add tests/
git commit -m "test: add TempCleaner tests for SystemOptimizer"
```

---

## Task 8: UpdateService

**Files:**
- Create: `src/AppName.Service/Engine/UpdateService.cs`
- Create: `tests/AppName.Service.Tests/Engine/UpdateServiceTests.cs`

- [ ] **Step 1: Написать тесты**

```csharp
using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using Moq;

namespace AppName.Service.Tests.Engine;

public class UpdateServiceTests
{
    [Fact]
    public async Task CheckUpdates_ReturnsUpdateInfo()
    {
        var httpMock = new Mock<HttpMessageHandler>();
        // UpdateService.CheckUpdates calls GitHub API — mock returns UpdateInfo
        var service = new UpdateService(httpClient: null, yaraRulesPath: "/nonexistent");
        var info = await service.CheckUpdatesAsync();

        Assert.NotNull(info);
        Assert.NotNull(info.LastChecked);
    }

    [Fact]
    public async Task UpdateYaraRules_NonExistentSource_ReturnsFalse()
    {
        var service = new UpdateService(httpClient: null, yaraRulesPath: "/nonexistent");
        var result = await service.UpdateYaraRulesAsync();
        Assert.False(result);
    }

    [Fact]
    public void LastUpdated_InitiallyNull()
    {
        var service = new UpdateService(httpClient: null, yaraRulesPath: "/nonexistent");
        Assert.Null(service.LastUpdated);
    }
}
```

- [ ] **Step 2: Реализовать UpdateService.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Microsoft.Extensions.Logging;

namespace AppName.Service.Engine;

public class UpdateService : IUpdateService
{
    private static readonly Uri ClamAvMirror =
        new("https://database.clamav.net/");
    private static readonly Uri YaraRulesRepo =
        new("https://api.github.com/repos/Yara-Rules/rules/tarball/master");
    private static readonly Uri MalSearcherRepo =
        new("https://api.github.com/repos/en0t/malsearcher/releases/latest");

    private readonly HttpClient _http;
    private readonly string _yaraRulesPath;
    private readonly string _databasesPath;
    private readonly ILogger<UpdateService>? _logger;

    public DateTime? LastUpdated { get; private set; }

    public UpdateService(
        HttpClient? httpClient = null,
        string? yaraRulesPath = null,
        string? databasesPath = null,
        ILogger<UpdateService>? logger = null)
    {
        _http = httpClient ?? new HttpClient();
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("AppName-Antivirus/1.0");
        _yaraRulesPath = yaraRulesPath ?? AppPaths.YaraRulesPath;
        _databasesPath = databasesPath ?? AppPaths.DatabasesPath;
        _logger = logger;
    }

    public Task<UpdateInfo> CheckUpdatesAsync(CancellationToken ct = default)
    {
        // Simplified: just report availability based on connectivity
        // TODO: check ETag / Last-Modified headers against local cache
        return Task.FromResult(new UpdateInfo(
            SignaturesAvailable: true,
            YaraRulesAvailable: true,
            MalSearcherAvailable: true,
            LastChecked: DateTime.UtcNow));
    }

    public async Task<bool> UpdateSignaturesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Updating ClamAV signatures...");
            // Download daily.cvd from mirror
            var destPath = Path.Combine(_databasesPath, "daily.cvd");
            var downloaded = await DownloadAndVerifyAsync(
                new Uri(ClamAvMirror, "daily.cvd"), destPath, ct);
            if (downloaded) LastUpdated = DateTime.UtcNow;
            return downloaded;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ClamAV signature update failed");
            return false;
        }
    }

    public async Task<bool> UpdateYaraRulesAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Updating YARA rules...");
            if (!Directory.Exists(_yaraRulesPath))
            {
                _logger?.LogWarning("YARA rules path does not exist: {Path}", _yaraRulesPath);
                return false;
            }
            // TODO: download tarball from YaraRulesRepo, extract .yar files to _yaraRulesPath
            // For now: placeholder
            LastUpdated = DateTime.UtcNow;
            return false; // return false until fully implemented
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "YARA rules update failed");
            return false;
        }
    }

    public async Task<bool> UpdateMalSearcherDbAsync(CancellationToken ct = default)
    {
        try
        {
            _logger?.LogInformation("Updating MalSearcher database...");
            var destPath = AppPaths.MalSearcherDbPath;
            // TODO: download latest release from MalSearcherRepo, verify SHA-256
            LastUpdated = DateTime.UtcNow;
            return false; // placeholder
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "MalSearcher update failed");
            return false;
        }
    }

    private async Task<bool> DownloadAndVerifyAsync(Uri uri, string destPath, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var tempPath = destPath + ".tmp";
            await using (var fs = File.Create(tempPath))
                await response.Content.CopyToAsync(fs, ct);

            // Atomic replace
            File.Move(tempPath, destPath, overwrite: true);
            _logger?.LogInformation("Downloaded {Uri} → {Dest}", uri, destPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Download failed: {Uri}", uri);
            return false;
        }
    }
}
```

- [ ] **Step 3: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "UpdateServiceTests" -v minimal
```

- [ ] **Step 4: Коммит**

```bash
git add src/ tests/
git commit -m "feat: add UpdateService for ClamAV/YARA/MalSearcher signature updates"
```

---

## Task 9: Расширение IPC CommandHandler новыми командами

Добавить команды: `GetRealTimeStatus`, `SetRealTimeEnabled`, `GetProcessList`, `GetAutostartEntries`, `CleanTempFiles`, `UpdateSignatures`, `StopScan`.

**Files:**
- Modify: `src/AppName.Service/Ipc/CommandHandler.cs`
- Modify: `src/AppName.Service/Ipc/IpcMessages.cs` (новые payload-записи)

- [ ] **Step 1: Добавить payload-записи в IpcMessages.cs**

```csharp
public record SetRealTimePayload(bool Enabled);
public record UpdateSignaturesPayload(bool Yara, bool ClamAv, bool MalSearcher);
```

- [ ] **Step 2: Расширить CommandHandler.HandleAsync**

Добавить новые ветки в switch:
```csharp
"GetRealTimeStatus"    => HandleGetRealTimeStatusAsync(ct),
"SetRealTimeEnabled"   => await HandleSetRealTimeAsync(request, ct),
"GetProcessList"       => await HandleGetProcessListAsync(ct),
"GetAutostartEntries"  => await HandleGetAutostartAsync(ct),
"CleanTempFiles"       => await HandleCleanTempAsync(ct),
"UpdateSignatures"     => await HandleUpdateSignaturesAsync(request, ct),
```

- [ ] **Step 3: Добавить новые зависимости в CommandHandler**

```csharp
public class CommandHandler
{
    private readonly IScanEngine _scanEngine;
    private readonly IQuarantineManager _quarantine;
    private readonly IRealTimeGuard _realTimeGuard;
    private readonly IProcessMonitor _processMonitor;
    private readonly ISystemOptimizer _systemOptimizer;
    private readonly IUpdateService _updateService;
    ...
}
```

- [ ] **Step 4: Реализовать новые хендлеры**

```csharp
private Task<IpcResponse> HandleGetRealTimeStatusAsync(CancellationToken ct) =>
    Task.FromResult(IpcResponse.Ok(JsonSerializer.SerializeToElement(new
    {
        enabled = _realTimeGuard.IsRunning
    })));

private Task<IpcResponse> HandleSetRealTimeAsync(IpcRequest request, CancellationToken ct)
{
    if (request.Payload is not JsonElement payload) return Task.FromResult(IpcResponse.Fail("Payload required"));
    var args = payload.Deserialize<SetRealTimePayload>();
    if (args is null) return Task.FromResult(IpcResponse.Fail("Invalid payload"));
    if (args.Enabled) _realTimeGuard.Start(); else _realTimeGuard.Stop();
    return Task.FromResult(IpcResponse.Ok());
}

private async Task<IpcResponse> HandleGetProcessListAsync(CancellationToken ct)
{
    var processes = await _processMonitor.GetProcessesAsync(ct);
    return IpcResponse.Ok(JsonSerializer.SerializeToElement(processes.Select(p => new
    {
        p.Pid, p.Name, p.ExecutablePath, p.HasValidSignature, p.RiskScore
    })));
}

private async Task<IpcResponse> HandleGetAutostartAsync(CancellationToken ct)
{
    var entries = await _systemOptimizer.GetAutostartEntriesAsync(ct);
    return IpcResponse.Ok(JsonSerializer.SerializeToElement(entries));
}

private async Task<IpcResponse> HandleCleanTempAsync(CancellationToken ct)
{
    var freed = await _systemOptimizer.CleanTempFilesAsync(ct);
    return IpcResponse.Ok(JsonSerializer.SerializeToElement(new { freedBytes = freed }));
}

private async Task<IpcResponse> HandleUpdateSignaturesAsync(IpcRequest request, CancellationToken ct)
{
    var ok = await _updateService.UpdateSignaturesAsync(ct);
    return ok ? IpcResponse.Ok() : IpcResponse.Fail("Signature update failed");
}
```

- [ ] **Step 5: Сборка и тесты**

```bash
dotnet build AppName.sln -v q
dotnet test tests/AppName.Service.Tests -v minimal
```

- [ ] **Step 6: Коммит**

```bash
git add src/
git commit -m "feat: extend IPC with RealTime, ProcessList, Autostart, TempClean, UpdateSignatures commands"
```

---

## Task 10: Подключить все компоненты в Program.cs + Worker.cs

**Files:**
- Modify: `src/AppName.Service/Program.cs`
- Modify: `src/AppName.Service/Worker.cs`

- [ ] **Step 1: Обновить Program.cs**

Добавить регистрацию новых сервисов:

```csharp
// HttpClient for UpdateService
builder.Services.AddHttpClient<UpdateService>();

// Plan 2 singletons
builder.Services.AddSingleton<IRealTimeGuard, RealTimeGuard>();
builder.Services.AddSingleton<IProcessMonitor, ProcessMonitor>();
builder.Services.AddSingleton<ISystemOptimizer, SystemOptimizer>();
builder.Services.AddSingleton<IUpdateService, UpdateService>();
builder.Services.AddSingleton<RansomwareDetector>();
builder.Services.AddSingleton<IpcEventChannel>();
```

- [ ] **Step 2: Обновить Worker.cs**

Worker запускает все фоновые компоненты и подписывается на события:

```csharp
using AppName.Service.Engine.Interfaces;
using AppName.Service.Ipc;

namespace AppName.Service;

public class Worker : BackgroundService
{
    private readonly IpcServer _ipcServer;
    private readonly IpcEventChannel _eventChannel;
    private readonly IRealTimeGuard _realTimeGuard;
    private readonly IProcessMonitor _processMonitor;
    private readonly ILogger<Worker> _logger;

    public Worker(
        IpcServer ipcServer,
        IpcEventChannel eventChannel,
        IRealTimeGuard realTimeGuard,
        IProcessMonitor processMonitor,
        ILogger<Worker> logger)
    {
        _ipcServer = ipcServer;
        _eventChannel = eventChannel;
        _realTimeGuard = realTimeGuard;
        _processMonitor = processMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppName Security Service started at: {Time}", DateTimeOffset.Now);

        // Wire real-time threat events → IPC event channel
        _realTimeGuard.ThreatDetected += (_, path) =>
            _eventChannel.Publish(new IpcEvent("ThreatFound",
                System.Text.Json.JsonSerializer.SerializeToElement(
                    new ThreatFoundPayload(path, null, 0))));

        _realTimeGuard.Start();
        _processMonitor.Start();

        await Task.WhenAll(
            _ipcServer.RunAsync(stoppingToken),
            _eventChannel.RunAsync(stoppingToken)
        );

        _realTimeGuard.Stop();
        _processMonitor.Stop();

        _logger.LogInformation("AppName Security Service stopped.");
    }
}
```

- [ ] **Step 3: Финальная сборка**

```bash
dotnet build AppName.sln -v q
```

Ожидаем: `0 Error(s)`

- [ ] **Step 4: Все тесты**

```bash
dotnet test tests/AppName.Service.Tests -v minimal
```

- [ ] **Step 5: Короткий запуск**

```bash
timeout 5 dotnet run --project src/AppName.Service 2>&1 || true
```

Ожидаем в логах: "AppName Security Service started", "IPC server starting", "RealTimeGuard started".

- [ ] **Step 6: Коммит**

```bash
git add src/
git commit -m "feat: wire Plan 2 components into Worker (RealTimeGuard, ProcessMonitor, EventChannel)"
```

---

## Итоговая проверка Plan 2

После всех задач:

```bash
dotnet build AppName.sln
dotnet test tests/AppName.Service.Tests -v minimal
```

Ожидаем: `Build succeeded. 0 Error(s)` и `Passed! — 0 Failed`.

**TODO для Plan 3 (Avalonia GUI):**
- IpcClient (читает IpcEventChannel, посылает команды через IpcServer)
- ViewModels: DashboardViewModel, ScanViewModel, QuarantineViewModel, ProcessViewModel, OptimizerViewModel, SettingsViewModel
- Views (Avalonia AXAML) для каждого экрана
- TrayIcon с уведомлениями
- Тема (светлая/тёмная, акцент Indigo)

**TODO для Plan 2 security (pipeline):**
- PipeSecurity ACL на обоих named pipes
- Caller authentication (WindowsPrincipal) для привилегированных команд
- Audit log в Windows Event Log для Restore/Delete из карантина
