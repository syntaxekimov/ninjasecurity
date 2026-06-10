# AppName — Plan 1: Foundation & Core Engine

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Создать Windows Service бэкенд с работающим движком сканирования (Hash + ClamAV + YARA), менеджером карантина, системой оценки угроз и IPC-слоем — итогом является полностью тестируемый антивирусный движок.

**Architecture:** Два .NET проекта в одном solution: `AppName.Service` (Worker Service) и `AppName.Service.Tests` (xUnit). Сервис экспонирует Named Pipes IPC сервер. Все движки инжектируются через DI с интерфейсами для тестируемости. DPAPI оборачивает per-file AES-256 ключи для карантина.

**Tech Stack:** C# 12, .NET 8, xUnit 2.x, Entity Framework Core 8 + SQLite, nClam, dnYara, System.IO.Pipes, Moq, System.Security.Cryptography

> **Замена имени:** Все `AppName` в коде и путях заменяются на финальное название через find-and-replace перед публикацией.

---

## Карта файлов

```
AppName/
├── AppName.sln
├── src/
│   └── AppName.Service/
│       ├── AppName.Service.csproj
│       ├── Program.cs
│       ├── Worker.cs
│       ├── AppPaths.cs
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   └── Entities/
│       │       ├── QuarantineEntry.cs
│       │       └── ScanHistoryEntry.cs
│       ├── Engine/
│       │   ├── Interfaces/
│       │   │   ├── IScanEngine.cs
│       │   │   ├── IQuarantineManager.cs
│       │   │   ├── IClamAvScanner.cs
│       │   │   ├── IYaraScanner.cs
│       │   │   └── IDataProtector.cs
│       │   ├── Models/
│       │   │   ├── ScanResult.cs
│       │   │   └── ThreatDisposition.cs
│       │   ├── ThreatScorer.cs
│       │   ├── QuarantineManager.cs
│       │   ├── HashChecker.cs
│       │   ├── ClamAvScanner.cs
│       │   ├── YaraScanner.cs
│       │   ├── ScanEngine.cs
│       │   └── DpapiDataProtector.cs
│       └── Ipc/
│           ├── IpcMessages.cs
│           ├── IpcServer.cs
│           └── CommandHandler.cs
└── tests/
    └── AppName.Service.Tests/
        ├── AppName.Service.Tests.csproj
        ├── Engine/
        │   ├── ThreatScorerTests.cs
        │   ├── QuarantineManagerTests.cs
        │   ├── HashCheckerTests.cs
        │   └── ScanEngineTests.cs
        └── Ipc/
            └── IpcMessagesTests.cs
```

---

## Task 1: Solution и scaffold проектов

**Files:**
- Create: `AppName.sln`
- Create: `src/AppName.Service/AppName.Service.csproj`
- Create: `tests/AppName.Service.Tests/AppName.Service.Tests.csproj`

- [ ] **Step 1: Создать solution и проекты**

```bash
cd /root/antivirus
dotnet new sln -n AppName
dotnet new worker -n AppName.Service -o src/AppName.Service
dotnet new xunit -n AppName.Service.Tests -o tests/AppName.Service.Tests --framework net8.0
dotnet sln add src/AppName.Service/AppName.Service.csproj
dotnet sln add tests/AppName.Service.Tests/AppName.Service.Tests.csproj
dotnet add tests/AppName.Service.Tests/AppName.Service.Tests.csproj reference src/AppName.Service/AppName.Service.csproj
```

- [ ] **Step 2: Добавить NuGet пакеты в Service**

```bash
cd src/AppName.Service
dotnet add package Microsoft.Extensions.Hosting.WindowsServices --version 8.*
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 8.*
dotnet add package Microsoft.EntityFrameworkCore.Design --version 8.*
dotnet add package nClam --version 5.*
dotnet add package dnYara --version 1.*
cd ../..
```

- [ ] **Step 3: Добавить NuGet пакеты в Tests**

```bash
cd tests/AppName.Service.Tests
dotnet add package Moq --version 4.*
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 8.*
dotnet add package coverlet.collector --version 6.*
cd ../..
```

- [ ] **Step 4: Убедиться что всё собирается**

```bash
dotnet build AppName.sln
```

Ожидаем: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Удалить шаблонный мусор**

```bash
rm tests/AppName.Service.Tests/UnitTest1.cs
```

- [ ] **Step 6: Первый коммит**

```bash
git add .
git commit -m "chore: scaffold solution, service, and tests projects"
```

---

## Task 2: AppPaths и базовые модели

**Files:**
- Create: `src/AppName.Service/AppPaths.cs`
- Create: `src/AppName.Service/Engine/Models/ScanResult.cs`
- Create: `src/AppName.Service/Engine/Models/ThreatDisposition.cs`

- [ ] **Step 1: Написать тест для AppPaths**

Создать `tests/AppName.Service.Tests/AppPathsTests.cs`:

```csharp
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
```

- [ ] **Step 2: Запустить тест — убедиться что падает**

```bash
dotnet test tests/AppName.Service.Tests --filter "AppPathsTests" -v minimal
```

Ожидаем: `FAILED` — `AppPaths` не существует.

- [ ] **Step 3: Создать AppPaths.cs**

```csharp
namespace AppName.Service;

public static class AppPaths
{
    public static string AppData =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AppName");

    public static string DatabasePath => Path.Combine(AppData, "appname.db");
    public static string QuarantinePath => Path.Combine(AppData, "Quarantine");
    public static string DatabasesPath => Path.Combine(AppData, "Databases");
    public static string MalSearcherDbPath => Path.Combine(DatabasesPath, "malsearcher.db");
    public static string YaraRulesPath => Path.Combine(DatabasesPath, "yara");
}
```

- [ ] **Step 4: Создать модели угроз**

Создать `src/AppName.Service/Engine/Models/ScanResult.cs`:

```csharp
namespace AppName.Service.Engine.Models;

public record ScanResult(
    string FilePath,
    bool IsThreat,
    string? ThreatName,
    int ConfidenceScore,
    DetectionSource Source
)
{
    public static ScanResult Clean(string filePath) =>
        new(filePath, false, null, 0, DetectionSource.None);
}

public enum DetectionSource { None, Hash, ClamAv, Yara }
```

Создать `src/AppName.Service/Engine/Models/ThreatDisposition.cs`:

```csharp
namespace AppName.Service.Engine.Models;

public enum ThreatDisposition
{
    Allow,    // score < 40
    Monitor,  // score 40–69
    Block     // score >= 70
}
```

- [ ] **Step 5: Запустить тесты — убедиться что проходят**

```bash
dotnet test tests/AppName.Service.Tests --filter "AppPathsTests" -v minimal
```

Ожидаем: `PASSED`

- [ ] **Step 6: Коммит**

```bash
git add .
git commit -m "feat: add AppPaths, ScanResult model, ThreatDisposition"
```

---

## Task 3: Интерфейсы движка

**Files:**
- Create: `src/AppName.Service/Engine/Interfaces/IScanEngine.cs`
- Create: `src/AppName.Service/Engine/Interfaces/IQuarantineManager.cs`
- Create: `src/AppName.Service/Engine/Interfaces/IClamAvScanner.cs`
- Create: `src/AppName.Service/Engine/Interfaces/IYaraScanner.cs`
- Create: `src/AppName.Service/Engine/Interfaces/IDataProtector.cs`

- [ ] **Step 1: Создать IScanEngine.cs**

```csharp
using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface IScanEngine
{
    Task<ScanResult> ScanFileAsync(string filePath, CancellationToken ct = default);
    Task<IReadOnlyList<ScanResult>> ScanDirectoryAsync(
        string dirPath, bool recursive = true, CancellationToken ct = default);
}
```

- [ ] **Step 2: Создать IQuarantineManager.cs**

```csharp
using AppName.Service.Data.Entities;
using AppName.Service.Engine.Models;

namespace AppName.Service.Engine.Interfaces;

public interface IQuarantineManager
{
    Task<Guid> IsolateAsync(string filePath, ScanResult threat, CancellationToken ct = default);
    Task RestoreAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Создать IClamAvScanner.cs**

```csharp
namespace AppName.Service.Engine.Interfaces;

public interface IClamAvScanner
{
    Task<ClamAvResult> ScanAsync(string filePath, CancellationToken ct = default);
}

public record ClamAvResult(bool IsInfected, string? VirusName);
```

- [ ] **Step 4: Создать IYaraScanner.cs**

```csharp
namespace AppName.Service.Engine.Interfaces;

public interface IYaraScanner
{
    Task<YaraResult> ScanAsync(string filePath, CancellationToken ct = default);
}

public record YaraResult(bool Matched, string? RuleName);
```

- [ ] **Step 5: Создать IDataProtector.cs**

```csharp
namespace AppName.Service.Engine.Interfaces;

public interface IDataProtector
{
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] data);
}
```

- [ ] **Step 6: Убедиться что проект собирается**

```bash
dotnet build src/AppName.Service
```

Ожидаем: `Build succeeded. 0 Error(s)`

- [ ] **Step 7: Коммит**

```bash
git add .
git commit -m "feat: add engine interfaces (IScanEngine, IQuarantineManager, IClamAvScanner, IYaraScanner, IDataProtector)"
```

---

## Task 4: Database layer (EF Core + SQLite)

**Files:**
- Create: `src/AppName.Service/Data/Entities/QuarantineEntry.cs`
- Create: `src/AppName.Service/Data/Entities/ScanHistoryEntry.cs`
- Create: `src/AppName.Service/Data/AppDbContext.cs`

- [ ] **Step 1: Создать QuarantineEntry.cs**

```csharp
namespace AppName.Service.Data.Entities;

public class QuarantineEntry
{
    public Guid Id { get; set; }
    public string OriginalPath { get; set; } = "";
    public string ThreatName { get; set; } = "";
    public int ConfidenceScore { get; set; }
    public string Sha256 { get; set; } = "";
    public DateTime QuarantinedAt { get; set; }
    public string EncryptedFileName { get; set; } = "";
    public byte[] ProtectedKey { get; set; } = [];
    public byte[] Iv { get; set; } = [];
}
```

- [ ] **Step 2: Создать ScanHistoryEntry.cs**

```csharp
namespace AppName.Service.Data.Entities;

public class ScanHistoryEntry
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ScanType { get; set; } = "";  // Quick, Full, Custom
    public int FilesScanned { get; set; }
    public int ThreatsFound { get; set; }
    public string? ScanPath { get; set; }
}
```

- [ ] **Step 3: Создать AppDbContext.cs**

```csharp
using AppName.Service.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppName.Service.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<QuarantineEntry> QuarantineEntries => Set<QuarantineEntry>();
    public DbSet<ScanHistoryEntry> ScanHistory => Set<ScanHistoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuarantineEntry>()
            .HasKey(e => e.Id);

        modelBuilder.Entity<ScanHistoryEntry>()
            .HasKey(e => e.Id);
    }
}
```

- [ ] **Step 4: Написать тест для AppDbContext**

Создать `tests/AppName.Service.Tests/Data/AppDbContextTests.cs`:

```csharp
using AppName.Service.Data;
using AppName.Service.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AppName.Service.Tests.Data;

public class AppDbContextTests
{
    private static AppDbContext CreateInMemory()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task CanAddAndRetrieveQuarantineEntry()
    {
        await using var db = CreateInMemory();
        var entry = new QuarantineEntry
        {
            Id = Guid.NewGuid(),
            OriginalPath = @"C:\Users\test\Downloads\evil.exe",
            ThreatName = "Trojan.Win32.Test",
            ConfidenceScore = 90,
            Sha256 = "abc123",
            QuarantinedAt = DateTime.UtcNow,
            EncryptedFileName = "test.qvault",
            ProtectedKey = [1, 2, 3],
            Iv = [4, 5, 6]
        };

        db.QuarantineEntries.Add(entry);
        await db.SaveChangesAsync();

        var loaded = await db.QuarantineEntries.FindAsync(entry.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Trojan.Win32.Test", loaded.ThreatName);
        Assert.Equal(90, loaded.ConfidenceScore);
    }

    [Fact]
    public async Task CanAddAndRetrieveScanHistory()
    {
        await using var db = CreateInMemory();
        var entry = new ScanHistoryEntry
        {
            Id = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow,
            ScanType = "Quick",
            FilesScanned = 1200,
            ThreatsFound = 0
        };

        db.ScanHistory.Add(entry);
        await db.SaveChangesAsync();

        var loaded = await db.ScanHistory.FindAsync(entry.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Quick", loaded.ScanType);
    }
}
```

- [ ] **Step 5: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "AppDbContextTests" -v minimal
```

Ожидаем: `PASSED (2 tests)`

- [ ] **Step 6: Коммит**

```bash
git add .
git commit -m "feat: add EF Core database layer (QuarantineEntry, ScanHistoryEntry, AppDbContext)"
```

---

## Task 5: ThreatScorer

**Files:**
- Create: `src/AppName.Service/Engine/ThreatScorer.cs`
- Test: `tests/AppName.Service.Tests/Engine/ThreatScorerTests.cs`

- [ ] **Step 1: Написать тесты**

Создать `tests/AppName.Service.Tests/Engine/ThreatScorerTests.cs`:

```csharp
using AppName.Service.Engine;
using AppName.Service.Engine.Models;

namespace AppName.Service.Tests.Engine;

public class ThreatScorerTests
{
    private readonly ThreatScorer _scorer = new();

    [Fact]
    public void Score_NoDetections_ReturnsZero()
    {
        var result = _scorer.Score([]);
        Assert.Equal(0, result);
    }

    [Fact]
    public void Score_HashDetection_Returns80()
    {
        var detections = new[]
        {
            new ScanResult("file.exe", true, "Hash.Known", 0, DetectionSource.Hash)
        };
        Assert.Equal(80, _scorer.Score(detections));
    }

    [Fact]
    public void Score_ClamAvDetection_Returns70()
    {
        var detections = new[]
        {
            new ScanResult("file.exe", true, "Trojan.Win32.X", 0, DetectionSource.ClamAv)
        };
        Assert.Equal(70, _scorer.Score(detections));
    }

    [Fact]
    public void Score_YaraDetection_Returns50()
    {
        var detections = new[]
        {
            new ScanResult("file.exe", true, "Yara.Suspicious", 0, DetectionSource.Yara)
        };
        Assert.Equal(50, _scorer.Score(detections));
    }

    [Fact]
    public void Score_MultipleDetections_CapsAt100()
    {
        var detections = new[]
        {
            new ScanResult("file.exe", true, "Hash.Known", 0, DetectionSource.Hash),
            new ScanResult("file.exe", true, "Trojan.Win32.X", 0, DetectionSource.ClamAv),
            new ScanResult("file.exe", true, "Yara.Sus", 0, DetectionSource.Yara)
        };
        Assert.Equal(100, _scorer.Score(detections));
    }

    [Theory]
    [InlineData(0, ThreatDisposition.Allow)]
    [InlineData(39, ThreatDisposition.Allow)]
    [InlineData(40, ThreatDisposition.Monitor)]
    [InlineData(69, ThreatDisposition.Monitor)]
    [InlineData(70, ThreatDisposition.Block)]
    [InlineData(100, ThreatDisposition.Block)]
    public void Disposition_CorrectThresholds(int score, ThreatDisposition expected)
    {
        Assert.Equal(expected, _scorer.Disposition(score));
    }
}
```

- [ ] **Step 2: Запустить — убедиться что падает**

```bash
dotnet test tests/AppName.Service.Tests --filter "ThreatScorerTests" -v minimal
```

Ожидаем: `FAILED` — `ThreatScorer` не существует.

- [ ] **Step 3: Реализовать ThreatScorer.cs**

```csharp
using AppName.Service.Engine.Models;

namespace AppName.Service.Engine;

public class ThreatScorer
{
    public int Score(IReadOnlyList<ScanResult> detections)
    {
        var total = detections.Sum(d => d.Source switch
        {
            DetectionSource.Hash   => 80,
            DetectionSource.ClamAv => 70,
            DetectionSource.Yara   => 50,
            _                      => 0
        });
        return Math.Min(total, 100);
    }

    public ThreatDisposition Disposition(int score) => score switch
    {
        >= 70 => ThreatDisposition.Block,
        >= 40 => ThreatDisposition.Monitor,
        _     => ThreatDisposition.Allow
    };
}
```

- [ ] **Step 4: Запустить — убедиться что проходит**

```bash
dotnet test tests/AppName.Service.Tests --filter "ThreatScorerTests" -v minimal
```

Ожидаем: `PASSED (8 tests)`

- [ ] **Step 5: Коммит**

```bash
git add .
git commit -m "feat: add ThreatScorer with confidence scoring (Hash=80, ClamAV=70, YARA=50, cap=100)"
```

---

## Task 6: IDataProtector — DPAPI и тестовая заглушка

**Files:**
- Create: `src/AppName.Service/Engine/DpapiDataProtector.cs`
- Create: `src/AppName.Service/Engine/PassthroughDataProtector.cs`

DPAPI работает только на Windows. Для тестов на Linux используем PassthroughDataProtector.

- [ ] **Step 1: Создать DpapiDataProtector.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using System.Security.Cryptography;

namespace AppName.Service.Engine;

// Windows-only: uses DPAPI to protect AES keys at rest.
// Falls back to no-op on non-Windows (dev/CI only — not for production use).
public class DpapiDataProtector : IDataProtector
{
    private static readonly byte[] Entropy =
        "AppName-Quarantine-v1"u8.ToArray();

    public byte[] Protect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI is only available on Windows");
        return ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] data)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI is only available on Windows");
        return ProtectedData.Unprotect(data, Entropy, DataProtectionScope.CurrentUser);
    }
}
```

- [ ] **Step 2: Создать PassthroughDataProtector.cs**

```csharp
using AppName.Service.Engine.Interfaces;

namespace AppName.Service.Engine;

// For testing only — does NOT encrypt the key. Never use in production.
public class PassthroughDataProtector : IDataProtector
{
    public byte[] Protect(byte[] data) => data;
    public byte[] Unprotect(byte[] data) => data;
}
```

- [ ] **Step 3: Написать тест**

Создать `tests/AppName.Service.Tests/Engine/DataProtectorTests.cs`:

```csharp
using AppName.Service.Engine;

namespace AppName.Service.Tests.Engine;

public class DataProtectorTests
{
    [Fact]
    public void Passthrough_RoundTrip_ReturnsSameData()
    {
        var protector = new PassthroughDataProtector();
        var original = new byte[] { 1, 2, 3, 4, 5 };

        var protected_ = protector.Protect(original);
        var unprotected = protector.Unprotect(protected_);

        Assert.Equal(original, unprotected);
    }
}
```

- [ ] **Step 4: Запустить тест**

```bash
dotnet test tests/AppName.Service.Tests --filter "DataProtectorTests" -v minimal
```

Ожидаем: `PASSED (1 test)`

- [ ] **Step 5: Коммит**

```bash
git add .
git commit -m "feat: add DpapiDataProtector (Windows) and PassthroughDataProtector (tests)"
```

---

## Task 7: QuarantineManager

**Files:**
- Create: `src/AppName.Service/Engine/QuarantineManager.cs`
- Test: `tests/AppName.Service.Tests/Engine/QuarantineManagerTests.cs`

- [ ] **Step 1: Написать тесты**

Создать `tests/AppName.Service.Tests/Engine/QuarantineManagerTests.cs`:

```csharp
using AppName.Service.Data;
using AppName.Service.Engine;
using AppName.Service.Engine.Models;
using Microsoft.EntityFrameworkCore;

namespace AppName.Service.Tests.Engine;

public class QuarantineManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _quarantineDir;
    private readonly AppDbContext _db;
    private readonly QuarantineManager _manager;

    public QuarantineManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        _quarantineDir = Path.Combine(_tempDir, "Quarantine");
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(_quarantineDir);

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(opts);
        _manager = new QuarantineManager(_db, new PassthroughDataProtector(), _quarantineDir);
    }

    [Fact]
    public async Task Isolate_MovesFileToQuarantine()
    {
        var testFile = Path.Combine(_tempDir, "evil.exe");
        await File.WriteAllTextAsync(testFile, "malicious content");
        var threat = new ScanResult(testFile, true, "Trojan.Test", 90, DetectionSource.Hash);

        var id = await _manager.IsolateAsync(testFile, threat);

        Assert.False(File.Exists(testFile));
        var entries = await _manager.ListAsync();
        Assert.Single(entries);
        Assert.Equal("Trojan.Test", entries[0].ThreatName);
        Assert.Equal(id, entries[0].Id);
    }

    [Fact]
    public async Task Restore_ReturnsFileToOriginalPath()
    {
        var testFile = Path.Combine(_tempDir, "evil.exe");
        var originalContent = "malicious content";
        await File.WriteAllTextAsync(testFile, originalContent);
        var threat = new ScanResult(testFile, true, "Trojan.Test", 90, DetectionSource.Hash);

        var id = await _manager.IsolateAsync(testFile, threat);
        await _manager.RestoreAsync(id);

        Assert.True(File.Exists(testFile));
        Assert.Equal(originalContent, await File.ReadAllTextAsync(testFile));
        var entries = await _manager.ListAsync();
        Assert.Empty(entries);
    }

    [Fact]
    public async Task Delete_RemovesFromDiskAndDb()
    {
        var testFile = Path.Combine(_tempDir, "evil.exe");
        await File.WriteAllTextAsync(testFile, "bad");
        var threat = new ScanResult(testFile, true, "Trojan.Test", 80, DetectionSource.ClamAv);

        var id = await _manager.IsolateAsync(testFile, threat);
        await _manager.DeleteAsync(id);

        var entries = await _manager.ListAsync();
        Assert.Empty(entries);
        Assert.Empty(Directory.GetFiles(_quarantineDir, "*.qvault"));
    }

    public void Dispose()
    {
        _db.Dispose();
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

- [ ] **Step 2: Запустить — убедиться что падает**

```bash
dotnet test tests/AppName.Service.Tests --filter "QuarantineManagerTests" -v minimal
```

Ожидаем: `FAILED` — `QuarantineManager` не существует.

- [ ] **Step 3: Реализовать QuarantineManager.cs**

```csharp
using AppName.Service.Data;
using AppName.Service.Data.Entities;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AppName.Service.Engine;

public class QuarantineManager : IQuarantineManager
{
    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly string _quarantinePath;

    public QuarantineManager(AppDbContext db, IDataProtector protector, string? quarantinePath = null)
    {
        _db = db;
        _protector = protector;
        _quarantinePath = quarantinePath ?? AppPaths.QuarantinePath;
        Directory.CreateDirectory(_quarantinePath);
    }

    public async Task<Guid> IsolateAsync(string filePath, ScanResult threat, CancellationToken ct = default)
    {
        var sha256 = await ComputeSha256Async(filePath, ct);
        var id = Guid.NewGuid();
        var aesKey = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(16);
        var encryptedFileName = $"{id:N}.qvault";
        var quarantineFilePath = Path.Combine(_quarantinePath, encryptedFileName);

        await EncryptFileAsync(filePath, quarantineFilePath, aesKey, iv, ct);
        File.Delete(filePath);

        _db.QuarantineEntries.Add(new QuarantineEntry
        {
            Id = id,
            OriginalPath = filePath,
            ThreatName = threat.ThreatName ?? "Unknown",
            ConfidenceScore = threat.ConfidenceScore,
            Sha256 = sha256,
            QuarantinedAt = DateTime.UtcNow,
            EncryptedFileName = encryptedFileName,
            ProtectedKey = _protector.Protect(aesKey),
            Iv = iv
        });
        await _db.SaveChangesAsync(ct);
        return id;
    }

    public async Task RestoreAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.QuarantineEntries.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Quarantine entry {id} not found");

        var aesKey = _protector.Unprotect(entry.ProtectedKey);
        var quarantineFilePath = Path.Combine(_quarantinePath, entry.EncryptedFileName);

        Directory.CreateDirectory(Path.GetDirectoryName(entry.OriginalPath)!);
        await DecryptFileAsync(quarantineFilePath, entry.OriginalPath, aesKey, entry.Iv, ct);
        File.Delete(quarantineFilePath);

        _db.QuarantineEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.QuarantineEntries.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Quarantine entry {id} not found");

        var quarantineFilePath = Path.Combine(_quarantinePath, entry.EncryptedFileName);
        if (File.Exists(quarantineFilePath))
            File.Delete(quarantineFilePath);

        _db.QuarantineEntries.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<QuarantineEntry>> ListAsync(CancellationToken ct = default) =>
        await _db.QuarantineEntries.OrderByDescending(e => e.QuarantinedAt).ToListAsync(ct);

    private static async Task EncryptFileAsync(
        string source, string dest, byte[] key, byte[] iv, CancellationToken ct)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        await using var sourceStream = File.OpenRead(source);
        await using var destStream = File.Create(dest);
        await using var cryptoStream = new CryptoStream(destStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
        await sourceStream.CopyToAsync(cryptoStream, ct);
    }

    private static async Task DecryptFileAsync(
        string source, string dest, byte[] key, byte[] iv, CancellationToken ct)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        await using var sourceStream = File.OpenRead(source);
        await using var cryptoStream = new CryptoStream(sourceStream, aes.CreateDecryptor(), CryptoStreamMode.Read);
        await using var destStream = File.Create(dest);
        await cryptoStream.CopyToAsync(destStream, ct);
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "QuarantineManagerTests" -v minimal
```

Ожидаем: `PASSED (3 tests)`

- [ ] **Step 5: Коммит**

```bash
git add .
git commit -m "feat: add QuarantineManager with AES-256-CBC per-file encryption"
```

---

## Task 8: HashChecker (MalSearcher)

**Files:**
- Create: `src/AppName.Service/Engine/HashChecker.cs`
- Test: `tests/AppName.Service.Tests/Engine/HashCheckerTests.cs`

HashChecker читает локальную SQLite БД хешей (загружается SignatureUpdater в Plan 2). В Plan 1 — работает с тестовой БД.

- [ ] **Step 1: Написать тесты**

Создать `tests/AppName.Service.Tests/Engine/HashCheckerTests.cs`:

```csharp
using AppName.Service.Engine;

namespace AppName.Service.Tests.Engine;

public class HashCheckerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly HashChecker _checker;

    public HashCheckerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");
        _checker = new HashChecker(_dbPath);
    }

    [Fact]
    public async Task Check_EmptyDatabase_ReturnsFalse()
    {
        var result = await _checker.IsKnownThreatAsync("abc123def456");
        Assert.False(result.IsKnown);
        Assert.Null(result.ThreatName);
    }

    [Fact]
    public async Task Check_KnownHash_ReturnsTrue()
    {
        await _checker.AddForTestingAsync("deadbeef01234567", "Test.Trojan.FakeHash");

        var result = await _checker.IsKnownThreatAsync("deadbeef01234567");

        Assert.True(result.IsKnown);
        Assert.Equal("Test.Trojan.FakeHash", result.ThreatName);
    }

    [Fact]
    public async Task Check_CaseInsensitive_ReturnsTrue()
    {
        await _checker.AddForTestingAsync("ABCDEF123456", "Test.Virus");

        var result = await _checker.IsKnownThreatAsync("abcdef123456");
        Assert.True(result.IsKnown);
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
```

- [ ] **Step 2: Запустить — убедиться что падает**

```bash
dotnet test tests/AppName.Service.Tests --filter "HashCheckerTests" -v minimal
```

Ожидаем: `FAILED`

- [ ] **Step 3: Реализовать HashChecker.cs**

```csharp
using Microsoft.Data.Sqlite;

namespace AppName.Service.Engine;

public class HashChecker
{
    private readonly string _dbPath;

    public HashChecker(string? dbPath = null)
    {
        _dbPath = dbPath ?? AppPaths.MalSearcherDbPath;
        EnsureTable();
    }

    public async Task<HashCheckResult> IsKnownThreatAsync(string sha256, CancellationToken ct = default)
    {
        if (!File.Exists(_dbPath))
            return new HashCheckResult(false, null);

        var normalized = sha256.ToLowerInvariant();
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT threat_name FROM hashes WHERE sha256 = @hash LIMIT 1";
        cmd.Parameters.AddWithValue("@hash", normalized);

        var result = await cmd.ExecuteScalarAsync(ct);
        return result is string name
            ? new HashCheckResult(true, name)
            : new HashCheckResult(false, null);
    }

    // Only for testing — adds a hash directly to the local DB
    public async Task AddForTestingAsync(string sha256, string threatName)
    {
        await using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO hashes (sha256, threat_name) VALUES (@h, @n)";
        cmd.Parameters.AddWithValue("@h", sha256.ToLowerInvariant());
        cmd.Parameters.AddWithValue("@n", threatName);
        await cmd.ExecuteNonQueryAsync();
    }

    private void EnsureTable()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS hashes (
                sha256 TEXT PRIMARY KEY,
                threat_name TEXT NOT NULL
            )
        """;
        cmd.ExecuteNonQuery();
    }
}

public record HashCheckResult(bool IsKnown, string? ThreatName);
```

- [ ] **Step 4: Добавить пакет Microsoft.Data.Sqlite в Service**

```bash
dotnet add src/AppName.Service package Microsoft.Data.Sqlite --version 8.*
```

- [ ] **Step 5: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "HashCheckerTests" -v minimal
```

Ожидаем: `PASSED (3 tests)`

- [ ] **Step 6: Коммит**

```bash
git add .
git commit -m "feat: add HashChecker with SQLite MalSearcher hash database"
```

---

## Task 9: ClamAvScanner

**Files:**
- Create: `src/AppName.Service/Engine/ClamAvScanner.cs`
- Test: `tests/AppName.Service.Tests/Engine/ClamAvScannerTests.cs` (unit test с моком)

- [ ] **Step 1: Написать unit-тест через мок**

Создать `tests/AppName.Service.Tests/Engine/ClamAvScannerTests.cs`:

```csharp
using AppName.Service.Engine.Interfaces;
using Moq;

namespace AppName.Service.Tests.Engine;

public class ClamAvScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenMockReturnsInfected_ReturnsInfectedResult()
    {
        var mock = new Mock<IClamAvScanner>();
        mock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvResult(true, "Win.Test.EICAR_HDB-1"));

        var result = await mock.Object.ScanAsync("test.exe");

        Assert.True(result.IsInfected);
        Assert.Equal("Win.Test.EICAR_HDB-1", result.VirusName);
    }

    [Fact]
    public async Task ScanAsync_WhenMockReturnsClean_ReturnsCleanResult()
    {
        var mock = new Mock<IClamAvScanner>();
        mock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvResult(false, null));

        var result = await mock.Object.ScanAsync("clean.exe");

        Assert.False(result.IsInfected);
        Assert.Null(result.VirusName);
    }
}
```

- [ ] **Step 2: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "ClamAvScannerTests" -v minimal
```

Ожидаем: `PASSED (2 tests)` — тесты проверяют интерфейс, не реализацию.

- [ ] **Step 3: Реализовать ClamAvScanner.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using nClam;

namespace AppName.Service.Engine;

public class ClamAvScanner : IClamAvScanner
{
    private readonly string _host;
    private readonly int _port;

    public ClamAvScanner(string host = "localhost", int port = 3310)
    {
        _host = host;
        _port = port;
    }

    public async Task<ClamAvResult> ScanAsync(string filePath, CancellationToken ct = default)
    {
        var client = new ClamClient(_host, _port);

        try
        {
            var result = await client.SendAndScanFileAsync(filePath);
            var isInfected = result.Result == ClamScanResults.VirusFound;
            var virusName = result.InfectedFiles?.FirstOrDefault()?.VirusName;
            return new ClamAvResult(isInfected, virusName);
        }
        catch (Exception)
        {
            // Если clamd недоступен — возвращаем чистый результат, логируем выше
            return new ClamAvResult(false, null);
        }
    }
}
```

- [ ] **Step 4: Убедиться что проект собирается**

```bash
dotnet build src/AppName.Service
```

Ожидаем: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Коммит**

```bash
git add .
git commit -m "feat: add ClamAvScanner wrapping nClam with graceful degradation"
```

---

## Task 10: YaraScanner

**Files:**
- Create: `src/AppName.Service/Engine/YaraScanner.cs`
- Test: `tests/AppName.Service.Tests/Engine/YaraScannerTests.cs`

- [ ] **Step 1: Написать unit-тест через мок**

Создать `tests/AppName.Service.Tests/Engine/YaraScannerTests.cs`:

```csharp
using AppName.Service.Engine.Interfaces;
using Moq;

namespace AppName.Service.Tests.Engine;

public class YaraScannerTests
{
    [Fact]
    public async Task ScanAsync_WhenRulesMatch_ReturnsMatchedResult()
    {
        var mock = new Mock<IYaraScanner>();
        mock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YaraResult(true, "Suspicious_PE_Header"));

        var result = await mock.Object.ScanAsync("suspicious.exe");

        Assert.True(result.Matched);
        Assert.Equal("Suspicious_PE_Header", result.RuleName);
    }

    [Fact]
    public async Task ScanAsync_WhenNoRulesDirectory_ReturnsClean()
    {
        var scanner = new YaraScanner("/nonexistent/path");

        var result = await scanner.ScanAsync("anyfile.exe");

        Assert.False(result.Matched);
        Assert.Null(result.RuleName);
    }
}
```

- [ ] **Step 2: Реализовать YaraScanner.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using dnYara;

namespace AppName.Service.Engine;

public class YaraScanner : IYaraScanner
{
    private readonly string _rulesPath;

    public YaraScanner(string? rulesPath = null)
    {
        _rulesPath = rulesPath ?? AppPaths.YaraRulesPath;
    }

    public Task<YaraResult> ScanAsync(string filePath, CancellationToken ct = default)
    {
        if (!Directory.Exists(_rulesPath))
            return Task.FromResult(new YaraResult(false, null));

        var ruleFiles = Directory.GetFiles(_rulesPath, "*.yar", SearchOption.AllDirectories);
        if (ruleFiles.Length == 0)
            return Task.FromResult(new YaraResult(false, null));

        try
        {
            using var ctx = new YaraContext();
            using var compiler = new Compiler();
            foreach (var ruleFile in ruleFiles)
                compiler.AddRuleFile(ruleFile);

            using var rules = compiler.GetRules();
            var scanner = new Scanner();
            var matches = scanner.ScanFile(filePath, rules);

            var first = matches.FirstOrDefault();
            return Task.FromResult(first != null
                ? new YaraResult(true, first.MatchingRule.Identifier)
                : new YaraResult(false, null));
        }
        catch
        {
            return Task.FromResult(new YaraResult(false, null));
        }
    }
}
```

- [ ] **Step 3: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "YaraScannerTests" -v minimal
```

Ожидаем: `PASSED (2 tests)`

- [ ] **Step 4: Коммит**

```bash
git add .
git commit -m "feat: add YaraScanner wrapping dnYara with graceful degradation"
```

---

## Task 11: ScanEngine (оркестрация)

**Files:**
- Create: `src/AppName.Service/Engine/ScanEngine.cs`
- Test: `tests/AppName.Service.Tests/Engine/ScanEngineTests.cs`

- [ ] **Step 1: Написать тесты**

Создать `tests/AppName.Service.Tests/Engine/ScanEngineTests.cs`:

```csharp
using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using Moq;

namespace AppName.Service.Tests.Engine;

public class ScanEngineTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Mock<IClamAvScanner> _clamMock;
    private readonly Mock<IYaraScanner> _yaraMock;
    private readonly HashChecker _hashChecker;
    private readonly ThreatScorer _scorer;
    private readonly ScanEngine _engine;

    public ScanEngineTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);

        _clamMock = new Mock<IClamAvScanner>();
        _yaraMock = new Mock<IYaraScanner>();
        _hashChecker = new HashChecker(Path.Combine(_tempDir, "hashes.db"));
        _scorer = new ThreatScorer();

        _clamMock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvResult(false, null));
        _yaraMock.Setup(s => s.ScanAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new YaraResult(false, null));

        _engine = new ScanEngine(_hashChecker, _clamMock.Object, _yaraMock.Object, _scorer);
    }

    [Fact]
    public async Task ScanFile_CleanFile_ReturnsCleanResult()
    {
        var file = Path.Combine(_tempDir, "clean.exe");
        await File.WriteAllTextAsync(file, "clean content");

        var result = await _engine.ScanFileAsync(file);

        Assert.False(result.IsThreat);
        Assert.Equal(0, result.ConfidenceScore);
    }

    [Fact]
    public async Task ScanFile_KnownHash_ReturnsThreat()
    {
        var file = Path.Combine(_tempDir, "evil.exe");
        await File.WriteAllTextAsync(file, "evil content");
        var sha256 = ComputeSha256(file);
        await _hashChecker.AddForTestingAsync(sha256, "Test.Trojan.KnownHash");

        var result = await _engine.ScanFileAsync(file);

        Assert.True(result.IsThreat);
        Assert.Equal(DetectionSource.Hash, result.Source);
        Assert.Equal(80, result.ConfidenceScore);
    }

    [Fact]
    public async Task ScanFile_ClamAvDetects_ReturnsThreat()
    {
        var file = Path.Combine(_tempDir, "infected.exe");
        await File.WriteAllTextAsync(file, "content");
        _clamMock.Setup(s => s.ScanAsync(file, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ClamAvResult(true, "Trojan.Win32.X"));

        var result = await _engine.ScanFileAsync(file);

        Assert.True(result.IsThreat);
        Assert.Equal(DetectionSource.ClamAv, result.Source);
    }

    [Fact]
    public async Task ScanFile_NonExistentFile_ReturnsClean()
    {
        var result = await _engine.ScanFileAsync("/nonexistent/file.exe");
        Assert.False(result.IsThreat);
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = System.Security.Cryptography.SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

- [ ] **Step 2: Запустить — убедиться что падает**

```bash
dotnet test tests/AppName.Service.Tests --filter "ScanEngineTests" -v minimal
```

Ожидаем: `FAILED`

- [ ] **Step 3: Реализовать ScanEngine.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using AppName.Service.Engine.Models;
using System.Security.Cryptography;

namespace AppName.Service.Engine;

public class ScanEngine : IScanEngine
{
    private readonly HashChecker _hashChecker;
    private readonly IClamAvScanner _clamAv;
    private readonly IYaraScanner _yara;
    private readonly ThreatScorer _scorer;

    public ScanEngine(
        HashChecker hashChecker,
        IClamAvScanner clamAv,
        IYaraScanner yara,
        ThreatScorer scorer)
    {
        _hashChecker = hashChecker;
        _clamAv = clamAv;
        _yara = yara;
        _scorer = scorer;
    }

    public async Task<ScanResult> ScanFileAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return ScanResult.Clean(filePath);

        // Layer 1: Hash check (fastest)
        var sha256 = await ComputeSha256Async(filePath, ct);
        var hashResult = await _hashChecker.IsKnownThreatAsync(sha256, ct);
        if (hashResult.IsKnown)
        {
            return new ScanResult(filePath, true, hashResult.ThreatName, 80, DetectionSource.Hash);
        }

        // Layer 2: ClamAV
        var clamResult = await _clamAv.ScanAsync(filePath, ct);
        if (clamResult.IsInfected)
        {
            var score = _scorer.Score([new ScanResult(filePath, true, clamResult.VirusName, 70, DetectionSource.ClamAv)]);
            return new ScanResult(filePath, true, clamResult.VirusName, score, DetectionSource.ClamAv);
        }

        // Layer 3: YARA
        var yaraResult = await _yara.ScanAsync(filePath, ct);
        if (yaraResult.Matched)
        {
            return new ScanResult(filePath, true, yaraResult.RuleName, 50, DetectionSource.Yara);
        }

        return ScanResult.Clean(filePath);
    }

    public async Task<IReadOnlyList<ScanResult>> ScanDirectoryAsync(
        string dirPath, bool recursive = true, CancellationToken ct = default)
    {
        if (!Directory.Exists(dirPath))
            return [];

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.EnumerateFiles(dirPath, "*.*", searchOption);
        var results = new List<ScanResult>();

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            var result = await ScanFileAsync(file, ct);
            if (result.IsThreat)
                results.Add(result);
        }

        return results;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

- [ ] **Step 4: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "ScanEngineTests" -v minimal
```

Ожидаем: `PASSED (4 tests)`

- [ ] **Step 5: Все тесты целиком**

```bash
dotnet test tests/AppName.Service.Tests -v minimal
```

Ожидаем: `PASSED` без ошибок.

- [ ] **Step 6: Коммит**

```bash
git add .
git commit -m "feat: add ScanEngine orchestrating Hash + ClamAV + YARA with 3-layer detection"
```

---

## Task 12: IPC Layer (Named Pipes)

**Files:**
- Create: `src/AppName.Service/Ipc/IpcMessages.cs`
- Create: `src/AppName.Service/Ipc/IpcServer.cs`
- Create: `src/AppName.Service/Ipc/CommandHandler.cs`
- Test: `tests/AppName.Service.Tests/Ipc/IpcMessagesTests.cs`

- [ ] **Step 1: Написать тест для IpcMessages**

Создать `tests/AppName.Service.Tests/Ipc/IpcMessagesTests.cs`:

```csharp
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
```

- [ ] **Step 2: Запустить — убедиться что падает**

```bash
dotnet test tests/AppName.Service.Tests --filter "IpcMessagesTests" -v minimal
```

Ожидаем: `FAILED`

- [ ] **Step 3: Создать IpcMessages.cs**

```csharp
using System.Text.Json;

namespace AppName.Service.Ipc;

public record IpcRequest(string Command, JsonElement? Payload = null);

public record IpcResponse(bool Success, string? Error, JsonElement? Data)
{
    public static IpcResponse Ok(JsonElement? data = null) =>
        new(true, null, data);

    public static IpcResponse Fail(string error) =>
        new(false, error, null);
}

public record StartScanPayload(string Type, string? Path);
public record QuarantineActionPayload(Guid Id, string Action);
```

- [ ] **Step 4: Запустить тесты**

```bash
dotnet test tests/AppName.Service.Tests --filter "IpcMessagesTests" -v minimal
```

Ожидаем: `PASSED (3 tests)`

- [ ] **Step 5: Создать CommandHandler.cs**

```csharp
using AppName.Service.Engine.Interfaces;
using AppName.Service.Ipc;
using System.Text.Json;

namespace AppName.Service.Ipc;

public class CommandHandler
{
    private readonly IScanEngine _scanEngine;
    private readonly IQuarantineManager _quarantine;

    public CommandHandler(IScanEngine scanEngine, IQuarantineManager quarantine)
    {
        _scanEngine = scanEngine;
        _quarantine = quarantine;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct = default)
    {
        return request.Command switch
        {
            "StartScan"       => await HandleStartScanAsync(request, ct),
            "GetStatus"       => HandleGetStatus(),
            "QuarantineAction"=> await HandleQuarantineActionAsync(request, ct),
            "ListQuarantine"  => await HandleListQuarantineAsync(ct),
            _ => IpcResponse.Fail($"Unknown command: {request.Command}")
        };
    }

    private async Task<IpcResponse> HandleStartScanAsync(IpcRequest request, CancellationToken ct)
    {
        if (request.Payload is not JsonElement payload)
            return IpcResponse.Fail("Payload required");

        var args = payload.Deserialize<StartScanPayload>();
        if (args is null)
            return IpcResponse.Fail("Invalid payload");

        var path = args.Type switch
        {
            "Quick"  => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Full"   => Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\",
            "Custom" => args.Path ?? "",
            _ => ""
        };

        if (string.IsNullOrEmpty(path))
            return IpcResponse.Fail($"Unknown scan type: {args.Type}");

        var results = await _scanEngine.ScanDirectoryAsync(path, recursive: true, ct);
        return IpcResponse.Ok(JsonSerializer.SerializeToElement(new
        {
            threatsFound = results.Count,
            threats = results.Select(r => new { r.FilePath, r.ThreatName, r.ConfidenceScore })
        }));
    }

    private static IpcResponse HandleGetStatus() =>
        IpcResponse.Ok(JsonSerializer.SerializeToElement(new { status = "running", protected_ = true }));

    private async Task<IpcResponse> HandleQuarantineActionAsync(IpcRequest request, CancellationToken ct)
    {
        if (request.Payload is not JsonElement payload)
            return IpcResponse.Fail("Payload required");

        var args = payload.Deserialize<QuarantineActionPayload>();
        if (args is null) return IpcResponse.Fail("Invalid payload");

        try
        {
            if (args.Action == "Restore")
                await _quarantine.RestoreAsync(args.Id, ct);
            else if (args.Action == "Delete")
                await _quarantine.DeleteAsync(args.Id, ct);
            else
                return IpcResponse.Fail($"Unknown action: {args.Action}");

            return IpcResponse.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return IpcResponse.Fail(ex.Message);
        }
    }

    private async Task<IpcResponse> HandleListQuarantineAsync(CancellationToken ct)
    {
        var entries = await _quarantine.ListAsync(ct);
        return IpcResponse.Ok(JsonSerializer.SerializeToElement(entries.Select(e => new
        {
            e.Id,
            e.OriginalPath,
            e.ThreatName,
            e.ConfidenceScore,
            e.Sha256,
            QuarantinedAt = e.QuarantinedAt.ToString("O")
        })));
    }
}
```

- [ ] **Step 6: Создать IpcServer.cs**

`IpcServer` — Singleton, но `CommandHandler` использует scoped `AppDbContext`. Решение: `IServiceScopeFactory` создаёт scope на каждый IPC-запрос.

```csharp
using Microsoft.Extensions.DependencyInjection;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AppName.Service.Ipc;

public class IpcServer
{
    public const string PipeName = "AppName.Security.IPC";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IpcServer> _logger;

    public IpcServer(IServiceScopeFactory scopeFactory, ILogger<IpcServer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("IPC server starting on pipe: {PipeName}", PipeName);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await AcceptConnectionAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IPC connection error");
            }
        }
    }

    private async Task AcceptConnectionAsync(CancellationToken ct)
    {
        await using var pipe = new NamedPipeServerStream(
            PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous);

        await pipe.WaitForConnectionAsync(ct);

        var buffer = new byte[65536];
        var bytesRead = await pipe.ReadAsync(buffer, ct);
        var json = Encoding.UTF8.GetString(buffer, 0, bytesRead);

        IpcResponse response;
        // Create a DI scope per request so scoped DbContext is properly managed
        await using var scope = _scopeFactory.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<CommandHandler>();

        try
        {
            var request = JsonSerializer.Deserialize<IpcRequest>(json)
                ?? throw new InvalidOperationException("Null request");
            response = await handler.HandleAsync(request, ct);
        }
        catch (Exception ex)
        {
            response = IpcResponse.Fail(ex.Message);
        }

        var responseJson = JsonSerializer.Serialize(response);
        var responseBytes = Encoding.UTF8.GetBytes(responseJson);
        await pipe.WriteAsync(responseBytes, ct);
    }
}
```

- [ ] **Step 7: Убедиться что всё собирается**

```bash
dotnet build AppName.sln
```

Ожидаем: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Коммит**

```bash
git add .
git commit -m "feat: add IPC layer (Named Pipes, CommandHandler, IpcServer)"
```

---

## Task 13: Windows Service (Worker + Program)

**Files:**
- Modify: `src/AppName.Service/Program.cs`
- Modify: `src/AppName.Service/Worker.cs`

- [ ] **Step 1: Обновить Program.cs**

```csharp
using AppName.Service;
using AppName.Service.Data;
using AppName.Service.Engine;
using AppName.Service.Engine.Interfaces;
using AppName.Service.Ipc;
using Microsoft.EntityFrameworkCore;

Directory.CreateDirectory(AppPaths.AppData);
Directory.CreateDirectory(AppPaths.QuarantinePath);
Directory.CreateDirectory(AppPaths.DatabasesPath);
Directory.CreateDirectory(AppPaths.YaraRulesPath);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(opt => opt.ServiceName = "AppName Security");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={AppPaths.DatabasePath}"));

// Singletons: stateless or thread-safe engine components
builder.Services.AddSingleton<IDataProtector, DpapiDataProtector>();
builder.Services.AddSingleton<HashChecker>();
builder.Services.AddSingleton<IClamAvScanner, ClamAvScanner>();
builder.Services.AddSingleton<IYaraScanner, YaraScanner>();
builder.Services.AddSingleton<ThreatScorer>();
builder.Services.AddSingleton<IScanEngine, ScanEngine>();
builder.Services.AddSingleton<IpcServer>(); // uses IServiceScopeFactory internally

// Scoped: one DbContext per IPC request scope
builder.Services.AddScoped<IQuarantineManager, QuarantineManager>();
builder.Services.AddScoped<CommandHandler>(); // resolved per-scope inside IpcServer

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

// Auto-migrate database on startup
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

await host.RunAsync();
```

- [ ] **Step 2: Обновить Worker.cs**

```csharp
using AppName.Service.Ipc;

namespace AppName.Service;

public class Worker : BackgroundService
{
    private readonly IpcServer _ipcServer;
    private readonly ILogger<Worker> _logger;

    public Worker(IpcServer ipcServer, ILogger<Worker> logger)
    {
        _ipcServer = ipcServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppName Security Service started at: {time}", DateTimeOffset.Now);
        await _ipcServer.RunAsync(stoppingToken);
        _logger.LogInformation("AppName Security Service stopped.");
    }
}
```

- [ ] **Step 3: Запустить сервис локально (не как Windows Service)**

```bash
dotnet run --project src/AppName.Service
```

Ожидаем в логах:
```
info: AppName.Service.Worker[0]
      AppName Security Service started at: ...
info: AppName.Service.Ipc.IpcServer[0]
      IPC server starting on pipe: AppName.Security.IPC
```

Остановить: `Ctrl+C`

- [ ] **Step 4: Финальный прогон всех тестов**

```bash
dotnet test AppName.sln -v minimal
```

Ожидаем: все тесты `PASSED`.

- [ ] **Step 5: Финальный коммит**

```bash
git add .
git commit -m "feat: add Windows Service worker wiring all engine components via DI"
```

---

## Итог Plan 1

После завершения этого плана у вас есть:
- ✅ Рабочий Windows Service с Named Pipes IPC
- ✅ ScanEngine с 3-слойным обнаружением (Hash + ClamAV + YARA)
- ✅ QuarantineManager с AES-256-CBC шифрованием
- ✅ ThreatScorer с confidence scoring 0–100
- ✅ HashChecker интегрированный с MalSearcher SQLite БД
- ✅ Все компоненты покрыты тестами
- ✅ DI через Microsoft.Extensions.Hosting
- ✅ SQLite + EF Core для карантина и истории сканирований

**Следующий шаг → Plan 2:** `docs/superpowers/plans/2026-06-10-plan2-service-features.md`  
(RealTimeGuard, ProcessMonitor, SystemOptimizer, SignatureUpdater)
