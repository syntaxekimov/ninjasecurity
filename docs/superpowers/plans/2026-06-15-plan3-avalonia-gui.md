# Ninja Security — Plan 3: Avalonia GUI

> **For agentic workers:** Use `superpowers:subagent-driven-development` to implement task-by-task.

**Goal:** Создать Avalonia-приложение `NinjaSecurity.App` с полноценным UI: Dashboard, Scan, Quarantine, Processes, Optimizer, Settings. TrayIcon. Двусторонний IPC с сервисом.

**Зависимости:** Plan 2 завершён. Сервис запускается, Named Pipes работают (`NinjaSecurity.IPC`, `NinjaSecurity.Events`).

---

## Карта файлов

```
src/NinjaSecurity.App/
├── NinjaSecurity.App.csproj
├── App.axaml
├── App.axaml.cs
├── Program.cs
├── Assets/
│   └── ninja-security.ico       (Task 1)
├── Ipc/
│   ├── IpcClient.cs             (Task 2)
│   └── IpcEventListener.cs      (Task 2)
├── ViewModels/
│   ├── MainWindowViewModel.cs   (Task 3)
│   ├── DashboardViewModel.cs    (Task 4)
│   ├── ScanViewModel.cs         (Task 5)
│   ├── QuarantineViewModel.cs   (Task 6)
│   ├── ProcessViewModel.cs      (Task 7)
│   ├── OptimizerViewModel.cs    (Task 8)
│   └── SettingsViewModel.cs     (Task 9)
├── Views/
│   ├── MainWindow.axaml         (Task 3)
│   ├── MainWindow.axaml.cs
│   ├── DashboardView.axaml      (Task 4)
│   ├── DashboardView.axaml.cs
│   ├── ScanView.axaml           (Task 5)
│   ├── ScanView.axaml.cs
│   ├── QuarantineView.axaml     (Task 6)
│   ├── QuarantineView.axaml.cs
│   ├── ProcessView.axaml        (Task 7)
│   ├── ProcessView.axaml.cs
│   ├── OptimizerView.axaml      (Task 8)
│   ├── OptimizerView.axaml.cs
│   ├── SettingsView.axaml       (Task 9)
│   └── SettingsView.axaml.cs
└── Styles/
    └── NinjaTheme.axaml         (Task 10)
```

---

## NuGet пакеты для App проекта

```xml
<PackageReference Include="Avalonia" Version="11.2.*" />
<PackageReference Include="Avalonia.Desktop" Version="11.2.*" />
<PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.*" />
<PackageReference Include="Avalonia.Fonts.Inter" Version="11.2.*" />
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
```

---

## Task 1: NinjaSecurity.App — проект и иконка

**Files:**
- Create: `src/NinjaSecurity.App/NinjaSecurity.App.csproj`
- Create: `src/NinjaSecurity.App/Program.cs`
- Create: `src/NinjaSecurity.App/App.axaml`
- Create: `src/NinjaSecurity.App/App.axaml.cs`
- Modify: `AppName.sln` — добавить новый проект

### Step 1: Создать директорию и .csproj

```bash
mkdir -p /root/antivirus/src/NinjaSecurity.App/Assets
mkdir -p /root/antivirus/src/NinjaSecurity.App/Ipc
mkdir -p /root/antivirus/src/NinjaSecurity.App/ViewModels
mkdir -p /root/antivirus/src/NinjaSecurity.App/Views
mkdir -p /root/antivirus/src/NinjaSecurity.App/Styles
```

Создать `src/NinjaSecurity.App/NinjaSecurity.App.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>NinjaSecurity.App</RootNamespace>
    <AssemblyName>NinjaSecurity.App</AssemblyName>
    <Product>Ninja Security</Product>
    <AssemblyTitle>Ninja Security</AssemblyTitle>
    <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.2.3" />
    <PackageReference Include="Avalonia.Desktop" Version="11.2.3" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.2.3" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.2.3" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.3.2" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.1" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.1" />
  </ItemGroup>
</Project>
```

### Step 2: Program.cs

```csharp
using Avalonia;

namespace NinjaSecurity.App;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

### Step 3: App.axaml

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="NinjaSecurity.App.App"
             RequestedThemeVariant="Dark">
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://NinjaSecurity.App/Styles/NinjaTheme.axaml" />
  </Application.Styles>
</Application>
```

### Step 4: App.axaml.cs

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NinjaSecurity.App.Views;
using NinjaSecurity.App.ViewModels;

namespace NinjaSecurity.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

### Step 5: Добавить в solution

```bash
cd /root/antivirus
dotnet sln AppName.sln add src/NinjaSecurity.App/NinjaSecurity.App.csproj
```

### Step 6: Создать заглушки Views и ViewModels (пустые файлы, чтобы проект собирался)

Минимальный `Views/MainWindow.axaml`:
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="NinjaSecurity.App.Views.MainWindow"
        Title="Ninja Security"
        Width="1100" Height="700">
  <TextBlock Text="Ninja Security" HorizontalAlignment="Center" VerticalAlignment="Center" />
</Window>
```

Минимальный `Views/MainWindow.axaml.cs`:
```csharp
using Avalonia.Controls;
namespace NinjaSecurity.App.Views;
public partial class MainWindow : Window { }
```

Минимальный `ViewModels/MainWindowViewModel.cs`:
```csharp
using CommunityToolkit.Mvvm.ComponentModel;
namespace NinjaSecurity.App.ViewModels;
public partial class MainWindowViewModel : ObservableObject { }
```

Минимальный `Styles/NinjaTheme.axaml`:
```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
</Styles>
```

### Step 7: Сборка

```bash
dotnet build AppName.sln -v q
```

Ожидаем `0 Error(s)`.

### Step 8: Коммит

```bash
git add src/NinjaSecurity.App/ AppName.sln
git commit -m "feat: add NinjaSecurity.App Avalonia project scaffold"
```

---

## Task 2: IpcClient + IpcEventListener

GUI общается с сервисом через два named pipe:
- `NinjaSecurity.IPC` — request/response (GUI пишет запрос, читает ответ)
- `NinjaSecurity.Events` — push события от сервиса (GUI только читает)

**Files:**
- Create: `src/NinjaSecurity.App/Ipc/IpcClient.cs`
- Create: `src/NinjaSecurity.App/Ipc/IpcEventListener.cs`
- Create: `src/NinjaSecurity.App/Ipc/IpcModels.cs`

### IpcModels.cs

```csharp
using System.Text.Json;

namespace NinjaSecurity.App.Ipc;

public record IpcRequest(string Command, JsonElement? Payload = null);
public record IpcResponse(bool Success, string? Error, JsonElement? Data);
public record IpcEvent(string EventType, JsonElement? Data = null);
```

### IpcClient.cs

```csharp
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NinjaSecurity.App.Ipc;

public class IpcClient
{
    public const string PipeName = "NinjaSecurity.IPC";

    public async Task<IpcResponse> SendAsync(
        string command,
        object? payload = null,
        CancellationToken ct = default)
    {
        try
        {
            await using var pipe = new NamedPipeClientStream(
                ".", PipeName, PipeDirection.InOut,
                PipeOptions.Asynchronous);

            await pipe.ConnectAsync(timeoutMs: 3000, ct);

            var request = new IpcRequest(
                command,
                payload is null ? null : JsonSerializer.SerializeToElement(payload));

            var json = JsonSerializer.Serialize(request);
            var bytes = Encoding.UTF8.GetBytes(json);
            await pipe.WriteAsync(bytes, ct);

            var buffer = new byte[65536];
            var read = await pipe.ReadAsync(buffer, ct);
            var responseJson = Encoding.UTF8.GetString(buffer, 0, read);
            return JsonSerializer.Deserialize<IpcResponse>(responseJson)
                   ?? new IpcResponse(false, "Empty response", null);
        }
        catch (Exception ex)
        {
            return new IpcResponse(false, ex.Message, null);
        }
    }

    public async Task<T?> GetDataAsync<T>(string command, object? payload = null, CancellationToken ct = default)
    {
        var response = await SendAsync(command, payload, ct);
        if (!response.Success || response.Data is null) return default;
        return response.Data.Value.Deserialize<T>();
    }
}
```

### IpcEventListener.cs

```csharp
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NinjaSecurity.App.Ipc;

public class IpcEventListener
{
    public const string EventPipeName = "NinjaSecurity.Events";

    public event EventHandler<IpcEvent>? EventReceived;

    private CancellationTokenSource? _cts;

    public void Start()
    {
        _cts = new CancellationTokenSource();
        _ = ListenLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var pipe = new NamedPipeClientStream(
                    ".", EventPipeName, PipeDirection.In,
                    PipeOptions.Asynchronous);

                await pipe.ConnectAsync(timeoutMs: 5000, ct);

                using var reader = new StreamReader(pipe, Encoding.UTF8);
                while (!ct.IsCancellationRequested && pipe.IsConnected)
                {
                    var line = await reader.ReadLineAsync(ct);
                    if (line is null) break;
                    var evt = JsonSerializer.Deserialize<IpcEvent>(line);
                    if (evt is not null)
                        EventReceived?.Invoke(this, evt);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { await Task.Delay(2000, ct); }
        }
    }
}
```

### Step: Сборка и коммит

```bash
dotnet build AppName.sln -v q
git add src/NinjaSecurity.App/
git commit -m "feat: add IpcClient and IpcEventListener for service communication"
```

---

## Task 3: MainWindow + NavigationSidebar

Главное окно: левая панель навигации + контентная область (ContentControl меняет view).

**Дизайн:**
- Ширина sidebar: 220px
- Иконки + текст для каждого раздела
- Активный раздел — выделен красным (#CC2222) или белым фоном
- Фон окна: тёмно-серый (#1A1A1A)
- Sidebar: чуть темнее (#141414)

**Files:**
- Rewrite: `Views/MainWindow.axaml` и `MainWindow.axaml.cs`
- Rewrite: `ViewModels/MainWindowViewModel.cs`

### MainWindowViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IpcClient _ipc = new();

    [ObservableProperty]
    private ObservableObject? _currentPage;

    [ObservableProperty]
    private string _selectedSection = "Dashboard";

    public DashboardViewModel Dashboard { get; }
    public ScanViewModel Scan { get; }
    public QuarantineViewModel Quarantine { get; }
    public ProcessViewModel Processes { get; }
    public OptimizerViewModel Optimizer { get; }
    public SettingsViewModel Settings { get; }

    public MainWindowViewModel()
    {
        Dashboard = new DashboardViewModel(_ipc);
        Scan = new ScanViewModel(_ipc);
        Quarantine = new QuarantineViewModel(_ipc);
        Processes = new ProcessViewModel(_ipc);
        Optimizer = new OptimizerViewModel(_ipc);
        Settings = new SettingsViewModel(_ipc);
        CurrentPage = Dashboard;
    }

    [RelayCommand]
    private void Navigate(string section)
    {
        SelectedSection = section;
        CurrentPage = section switch
        {
            "Dashboard"  => (ObservableObject)Dashboard,
            "Scan"       => Scan,
            "Quarantine" => Quarantine,
            "Processes"  => Processes,
            "Optimizer"  => Optimizer,
            "Settings"   => Settings,
            _            => Dashboard
        };
    }
}
```

### MainWindow.axaml

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:NinjaSecurity.App.ViewModels"
        xmlns:views="using:NinjaSecurity.App.Views"
        x:Class="NinjaSecurity.App.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="Ninja Security"
        Width="1100" Height="700"
        MinWidth="900" MinHeight="600"
        Background="#1A1A1A"
        ExtendClientAreaToDecorationsHint="True">

  <Grid ColumnDefinitions="220,*">

    <!-- Sidebar -->
    <Border Grid.Column="0" Background="#141414">
      <DockPanel>

        <!-- Logo -->
        <Border DockPanel.Dock="Top" Padding="20,24,20,16">
          <StackPanel Spacing="4">
            <TextBlock Text="⚔" FontSize="28" Foreground="#CC2222"
                       HorizontalAlignment="Center" />
            <TextBlock Text="NINJA SECURITY" FontSize="11" FontWeight="Bold"
                       Foreground="#AAAAAA" LetterSpacing="2"
                       HorizontalAlignment="Center" />
          </StackPanel>
        </Border>

        <Separator DockPanel.Dock="Top" Background="#2A2A2A" Height="1" />

        <!-- Nav items -->
        <StackPanel DockPanel.Dock="Top" Margin="8,8,8,0" Spacing="2">
          <Button Command="{Binding NavigateCommand}" CommandParameter="Dashboard"
                  Classes="NavButton" Classes.Active="{Binding SelectedSection, Converter={StaticResource EqConverter}, ConverterParameter=Dashboard}">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <TextBlock Text="⬡" FontSize="16" />
              <TextBlock Text="Dashboard" />
            </StackPanel>
          </Button>
          <Button Command="{Binding NavigateCommand}" CommandParameter="Scan"
                  Classes="NavButton" Classes.Active="{Binding SelectedSection, Converter={StaticResource EqConverter}, ConverterParameter=Scan}">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <TextBlock Text="🔍" FontSize="16" />
              <TextBlock Text="Сканирование" />
            </StackPanel>
          </Button>
          <Button Command="{Binding NavigateCommand}" CommandParameter="Quarantine"
                  Classes="NavButton" Classes.Active="{Binding SelectedSection, Converter={StaticResource EqConverter}, ConverterParameter=Quarantine}">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <TextBlock Text="🔒" FontSize="16" />
              <TextBlock Text="Карантин" />
            </StackPanel>
          </Button>
          <Button Command="{Binding NavigateCommand}" CommandParameter="Processes"
                  Classes="NavButton" Classes.Active="{Binding SelectedSection, Converter={StaticResource EqConverter}, ConverterParameter=Processes}">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <TextBlock Text="⚙" FontSize="16" />
              <TextBlock Text="Процессы" />
            </StackPanel>
          </Button>
          <Button Command="{Binding NavigateCommand}" CommandParameter="Optimizer"
                  Classes="NavButton" Classes.Active="{Binding SelectedSection, Converter={StaticResource EqConverter}, ConverterParameter=Optimizer}">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <TextBlock Text="🛡" FontSize="16" />
              <TextBlock Text="Оптимизатор" />
            </StackPanel>
          </Button>
          <Button Command="{Binding NavigateCommand}" CommandParameter="Settings"
                  Classes="NavButton" Classes.Active="{Binding SelectedSection, Converter={StaticResource EqConverter}, ConverterParameter=Settings}">
            <StackPanel Orientation="Horizontal" Spacing="10">
              <TextBlock Text="⚙" FontSize="16" />
              <TextBlock Text="Настройки" />
            </StackPanel>
          </Button>
        </StackPanel>

      </DockPanel>
    </Border>

    <!-- Content -->
    <ContentControl Grid.Column="1" Content="{Binding CurrentPage}" />

  </Grid>
</Window>
```

**Note:** Реализация `EqConverter` нужна как `IValueConverter` — добавить в `Converters/EqualityConverter.cs`.

### Коммит

```bash
git add src/NinjaSecurity.App/
git commit -m "feat: add MainWindow with sidebar navigation"
```

---

## Task 4: DashboardView + DashboardViewModel

Главный экран: статус защиты, кнопка быстрого сканирования, переключатель RealTime Guard, счётчик угроз.

### DashboardViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty]
    private bool _realTimeEnabled;

    [ObservableProperty]
    private string _statusText = "Проверка связи с сервисом...";

    [ObservableProperty]
    private bool _serviceConnected;

    [ObservableProperty]
    private int _threatsFound;

    public DashboardViewModel(IpcClient ipc)
    {
        _ipc = ipc;
        _ = LoadStatusAsync();
    }

    private async Task LoadStatusAsync()
    {
        var response = await _ipc.SendAsync("GetStatus");
        ServiceConnected = response.Success;

        if (response.Success)
        {
            var rtResponse = await _ipc.SendAsync("GetRealTimeStatus");
            if (rtResponse.Success && rtResponse.Data.HasValue)
                RealTimeEnabled = rtResponse.Data.Value.GetProperty("enabled").GetBoolean();
            StatusText = RealTimeEnabled ? "Защита активна" : "Защита отключена";
        }
        else
        {
            StatusText = "Сервис недоступен";
        }
    }

    [RelayCommand]
    private async Task ToggleRealTime()
    {
        var newState = !RealTimeEnabled;
        var response = await _ipc.SendAsync("SetRealTimeEnabled", new { Enabled = newState });
        if (response.Success)
        {
            RealTimeEnabled = newState;
            StatusText = newState ? "Защита активна" : "Защита отключена";
        }
    }

    [RelayCommand]
    private async Task QuickScan()
    {
        StatusText = "Быстрое сканирование...";
        var response = await _ipc.SendAsync("StartScan", new { Type = "Quick" });
        if (response.Success && response.Data.HasValue)
        {
            ThreatsFound = response.Data.Value.GetProperty("threatsFound").GetInt32();
            StatusText = ThreatsFound > 0
                ? $"Найдено угроз: {ThreatsFound}"
                : "Угрозы не обнаружены";
        }
    }
}
```

### DashboardView.axaml

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:NinjaSecurity.App.ViewModels"
             x:Class="NinjaSecurity.App.Views.DashboardView"
             x:DataType="vm:DashboardViewModel">

  <ScrollViewer>
    <StackPanel Margin="40" Spacing="24">

      <!-- Status Card -->
      <Border Classes="Card">
        <StackPanel Spacing="16" HorizontalAlignment="Center">
          <TextBlock Text="⚔" FontSize="64" HorizontalAlignment="Center"
                     Foreground="{Binding RealTimeEnabled, Converter={StaticResource BoolToColorConverter},
                                  ConverterParameter='#22CC55|#CC2222'}" />
          <TextBlock Text="{Binding StatusText}" FontSize="22" FontWeight="SemiBold"
                     HorizontalAlignment="Center" Foreground="White" />
          <ToggleButton IsChecked="{Binding RealTimeEnabled}"
                        Command="{Binding ToggleRealTimeCommand}"
                        HorizontalAlignment="Center"
                        Classes="RedToggle"
                        Content="{Binding RealTimeEnabled, Converter={StaticResource BoolToStringConverter},
                                  ConverterParameter='RealTime Guard: ВКЛ|RealTime Guard: ВЫКЛ'}" />
        </StackPanel>
      </Border>

      <!-- Stats Row -->
      <Grid ColumnDefinitions="*,*,*" ColumnSpacing="16">
        <Border Grid.Column="0" Classes="StatCard">
          <StackPanel HorizontalAlignment="Center" Spacing="4">
            <TextBlock Text="{Binding ThreatsFound}" FontSize="32" FontWeight="Bold"
                       Foreground="#CC2222" HorizontalAlignment="Center" />
            <TextBlock Text="Угроз найдено" Foreground="#888" FontSize="12"
                       HorizontalAlignment="Center" />
          </StackPanel>
        </Border>
        <Border Grid.Column="1" Classes="StatCard">
          <StackPanel HorizontalAlignment="Center" Spacing="4">
            <TextBlock Text="●" FontSize="32" HorizontalAlignment="Center"
                       Foreground="#22CC55" />
            <TextBlock Text="Базы актуальны" Foreground="#888" FontSize="12"
                       HorizontalAlignment="Center" />
          </StackPanel>
        </Border>
        <Border Grid.Column="2" Classes="StatCard">
          <StackPanel HorizontalAlignment="Center" Spacing="4">
            <TextBlock Text="⚡" FontSize="32" HorizontalAlignment="Center"
                       Foreground="#E8E8E8" />
            <TextBlock Text="Мониторинг активен" Foreground="#888" FontSize="12"
                       HorizontalAlignment="Center" />
          </StackPanel>
        </Border>
      </Grid>

      <!-- Quick Scan Button -->
      <Button Command="{Binding QuickScanCommand}"
              HorizontalAlignment="Stretch"
              Classes="PrimaryButton"
              Content="⚔  Быстрое сканирование" />

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

---

## Task 5: ScanView + ScanViewModel

Экран сканирования: выбор типа, прогресс, таблица результатов.

### ScanViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace NinjaSecurity.App.ViewModels;

public record ThreatResult(string FilePath, string? ThreatName, int Score);

public partial class ScanViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private string _selectedScanType = "Quick";
    [ObservableProperty] private string _customPath = "";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Готов к сканированию";
    [ObservableProperty] private bool _showCustomPath;

    public ObservableCollection<ThreatResult> Threats { get; } = [];

    public ScanViewModel(IpcClient ipc) => _ipc = ipc;

    partial void OnSelectedScanTypeChanged(string value) =>
        ShowCustomPath = value == "Custom";

    [RelayCommand]
    private async Task StartScan()
    {
        IsScanning = true;
        Threats.Clear();
        StatusText = "Сканирование...";

        var payload = SelectedScanType == "Custom"
            ? new { Type = "Custom", Path = CustomPath }
            : (object)new { Type = SelectedScanType };

        var response = await _ipc.SendAsync("StartScan", payload);

        if (response.Success && response.Data.HasValue)
        {
            var data = response.Data.Value;
            var count = data.GetProperty("threatsFound").GetInt32();
            var list = data.GetProperty("threats").Deserialize<List<ThreatResult>>() ?? [];
            foreach (var t in list) Threats.Add(t);
            StatusText = count > 0 ? $"Найдено угроз: {count}" : "Угрозы не обнаружены ✓";
        }
        else
        {
            StatusText = $"Ошибка: {response.Error}";
        }

        IsScanning = false;
    }
}
```

### ScanView.axaml

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:NinjaSecurity.App.ViewModels"
             x:Class="NinjaSecurity.App.Views.ScanView"
             x:DataType="vm:ScanViewModel">
  <DockPanel Margin="32">

    <!-- Header -->
    <StackPanel DockPanel.Dock="Top" Spacing="16" Margin="0,0,0,24">
      <TextBlock Text="Сканирование" FontSize="24" FontWeight="Bold" Foreground="White" />

      <!-- Scan type selector -->
      <StackPanel Orientation="Horizontal" Spacing="8">
        <RadioButton Content="Быстрое" GroupName="ScanType"
                     IsChecked="{Binding SelectedScanType, Converter={StaticResource EqConverter}, ConverterParameter=Quick}"
                     Command="{Binding}" CommandParameter="Quick" />
        <RadioButton Content="Полное" GroupName="ScanType"
                     IsChecked="{Binding SelectedScanType, Converter={StaticResource EqConverter}, ConverterParameter=Full}"
                     Command="{Binding}" CommandParameter="Full" />
        <RadioButton Content="Выборочное" GroupName="ScanType"
                     IsChecked="{Binding SelectedScanType, Converter={StaticResource EqConverter}, ConverterParameter=Custom}"
                     Command="{Binding}" CommandParameter="Custom" />
      </StackPanel>

      <TextBox IsVisible="{Binding ShowCustomPath}"
               Text="{Binding CustomPath}"
               Watermark="Путь к папке или файлу..."
               Classes="DarkInput" />

      <Button Command="{Binding StartScanCommand}"
              IsEnabled="{Binding !IsScanning}"
              Classes="PrimaryButton"
              Content="⚔  Начать сканирование" />

      <TextBlock Text="{Binding StatusText}" Foreground="#888" />
    </StackPanel>

    <!-- Results -->
    <DataGrid ItemsSource="{Binding Threats}"
              AutoGenerateColumns="False"
              IsReadOnly="True"
              Classes="DarkGrid">
      <DataGrid.Columns>
        <DataGridTextColumn Header="Файл" Binding="{Binding FilePath}" Width="*" />
        <DataGridTextColumn Header="Угроза" Binding="{Binding ThreatName}" Width="200" />
        <DataGridTextColumn Header="Уровень риска" Binding="{Binding Score}" Width="100" />
      </DataGrid.Columns>
    </DataGrid>

  </DockPanel>
</UserControl>
```

---

## Task 6: QuarantineView + QuarantineViewModel

### QuarantineViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace NinjaSecurity.App.ViewModels;

public record QuarantineItem(
    string Id, string OriginalPath, string? ThreatName,
    int ConfidenceScore, string Sha256, string QuarantinedAt);

public partial class QuarantineViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private QuarantineItem? _selectedItem;
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<QuarantineItem> Items { get; } = [];

    public QuarantineViewModel(IpcClient ipc)
    {
        _ipc = ipc;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var items = await _ipc.GetDataAsync<List<QuarantineItem>>("ListQuarantine") ?? [];
        Items.Clear();
        foreach (var item in items) Items.Add(item);
    }

    [RelayCommand]
    private async Task Restore()
    {
        if (SelectedItem is null) return;
        var response = await _ipc.SendAsync("QuarantineAction",
            new { Id = Guid.Parse(SelectedItem.Id), Action = "Restore" });
        StatusText = response.Success ? "Файл восстановлен" : $"Ошибка: {response.Error}";
        if (response.Success) await LoadAsync();
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedItem is null) return;
        var response = await _ipc.SendAsync("QuarantineAction",
            new { Id = Guid.Parse(SelectedItem.Id), Action = "Delete" });
        StatusText = response.Success ? "Файл удалён" : $"Ошибка: {response.Error}";
        if (response.Success) await LoadAsync();
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();
}
```

### QuarantineView.axaml

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:NinjaSecurity.App.ViewModels"
             x:Class="NinjaSecurity.App.Views.QuarantineView"
             x:DataType="vm:QuarantineViewModel">
  <DockPanel Margin="32">

    <StackPanel DockPanel.Dock="Top" Spacing="8" Margin="0,0,0,16">
      <TextBlock Text="Карантин" FontSize="24" FontWeight="Bold" Foreground="White" />
      <StackPanel Orientation="Horizontal" Spacing="8">
        <Button Command="{Binding RestoreCommand}" Content="↩ Восстановить"
                IsEnabled="{Binding SelectedItem, Converter={x:Static ObjectConverters.IsNotNull}}" />
        <Button Command="{Binding DeleteCommand}" Content="✕ Удалить"
                Classes="DangerButton"
                IsEnabled="{Binding SelectedItem, Converter={x:Static ObjectConverters.IsNotNull}}" />
        <Button Command="{Binding RefreshCommand}" Content="↺ Обновить" />
      </StackPanel>
      <TextBlock Text="{Binding StatusText}" Foreground="#888" />
    </StackPanel>

    <DataGrid ItemsSource="{Binding Items}"
              SelectedItem="{Binding SelectedItem}"
              AutoGenerateColumns="False"
              IsReadOnly="True"
              Classes="DarkGrid">
      <DataGrid.Columns>
        <DataGridTextColumn Header="Оригинальный путь" Binding="{Binding OriginalPath}" Width="*" />
        <DataGridTextColumn Header="Угроза" Binding="{Binding ThreatName}" Width="180" />
        <DataGridTextColumn Header="Риск" Binding="{Binding ConfidenceScore}" Width="60" />
        <DataGridTextColumn Header="Дата" Binding="{Binding QuarantinedAt}" Width="160" />
      </DataGrid.Columns>
    </DataGrid>

  </DockPanel>
</UserControl>
```

---

## Task 7: ProcessView + ProcessViewModel

### ProcessViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace NinjaSecurity.App.ViewModels;

public record ProcessItem(
    int Pid, string Name, string? ExecutablePath,
    bool HasValidSignature, int RiskScore);

public partial class ProcessViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _showHighRiskOnly;

    public ObservableCollection<ProcessItem> AllProcesses { get; } = [];
    public ObservableCollection<ProcessItem> Processes { get; } = [];

    public ProcessViewModel(IpcClient ipc)
    {
        _ipc = ipc;
        _ = LoadAsync();
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();
    partial void OnShowHighRiskOnlyChanged(bool value) => ApplyFilter();

    private void ApplyFilter()
    {
        var filtered = AllProcesses
            .Where(p => string.IsNullOrEmpty(FilterText) ||
                        p.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
            .Where(p => !ShowHighRiskOnly || p.RiskScore >= 50)
            .OrderByDescending(p => p.RiskScore);

        Processes.Clear();
        foreach (var p in filtered) Processes.Add(p);
    }

    [RelayCommand]
    private async Task Load() => await LoadAsync();

    private async Task LoadAsync()
    {
        var items = await _ipc.GetDataAsync<List<ProcessItem>>("GetProcessList") ?? [];
        AllProcesses.Clear();
        foreach (var p in items) AllProcesses.Add(p);
        ApplyFilter();
    }
}
```

### ProcessView.axaml

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:NinjaSecurity.App.ViewModels"
             x:Class="NinjaSecurity.App.Views.ProcessView"
             x:DataType="vm:ProcessViewModel">
  <DockPanel Margin="32">

    <StackPanel DockPanel.Dock="Top" Spacing="8" Margin="0,0,0,16">
      <TextBlock Text="Мониторинг процессов" FontSize="24" FontWeight="Bold" Foreground="White" />
      <StackPanel Orientation="Horizontal" Spacing="12">
        <TextBox Text="{Binding FilterText}" Watermark="Поиск по имени..."
                 Width="280" Classes="DarkInput" />
        <CheckBox Content="Только высокий риск (≥50)"
                  IsChecked="{Binding ShowHighRiskOnly}"
                  Foreground="White" />
        <Button Command="{Binding LoadCommand}" Content="↺ Обновить" />
      </StackPanel>
    </StackPanel>

    <DataGrid ItemsSource="{Binding Processes}"
              AutoGenerateColumns="False"
              IsReadOnly="True"
              Classes="DarkGrid">
      <DataGrid.Columns>
        <DataGridTextColumn Header="PID" Binding="{Binding Pid}" Width="60" />
        <DataGridTextColumn Header="Имя" Binding="{Binding Name}" Width="160" />
        <DataGridTextColumn Header="Путь" Binding="{Binding ExecutablePath}" Width="*" />
        <DataGridCheckBoxColumn Header="Подпись" Binding="{Binding HasValidSignature}" Width="70" />
        <DataGridTextColumn Header="Риск" Binding="{Binding RiskScore}" Width="60" />
      </DataGrid.Columns>
    </DataGrid>

  </DockPanel>
</UserControl>
```

---

## Task 8: OptimizerView + OptimizerViewModel

### OptimizerViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;
using System.Collections.ObjectModel;

namespace NinjaSecurity.App.ViewModels;

public record AutorunItem(
    string Id, string Name, string? ImagePath,
    string Location, bool IsEnabled, bool IsSigned, int RiskScore);

public partial class OptimizerViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private long _tempSizeBytes;

    public string TempSizeMb =>
        TempSizeBytes > 0 ? $"{TempSizeBytes / 1024.0 / 1024.0:F1} МБ" : "—";

    public ObservableCollection<AutorunItem> AutorunEntries { get; } = [];

    public OptimizerViewModel(IpcClient ipc)
    {
        _ipc = ipc;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var entries = await _ipc.GetDataAsync<List<AutorunItem>>("GetAutostartEntries") ?? [];
        AutorunEntries.Clear();
        foreach (var e in entries) AutorunEntries.Add(e);
    }

    [RelayCommand]
    private async Task CleanTemp()
    {
        StatusText = "Очистка...";
        var response = await _ipc.SendAsync("CleanTempFiles");
        if (response.Success && response.Data.HasValue)
        {
            var freed = response.Data.Value.GetProperty("freedBytes").GetInt64();
            StatusText = $"Очищено: {freed / 1024.0 / 1024.0:F1} МБ";
        }
        else
        {
            StatusText = $"Ошибка: {response.Error}";
        }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();
}
```

### OptimizerView.axaml

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:NinjaSecurity.App.ViewModels"
             x:Class="NinjaSecurity.App.Views.OptimizerView"
             x:DataType="vm:OptimizerViewModel">
  <DockPanel Margin="32">

    <StackPanel DockPanel.Dock="Top" Spacing="12" Margin="0,0,0,20">
      <TextBlock Text="Оптимизатор" FontSize="24" FontWeight="Bold" Foreground="White" />

      <!-- Temp cleaner card -->
      <Border Classes="Card" Padding="20">
        <StackPanel Spacing="8">
          <TextBlock Text="Очистка временных файлов" FontSize="14"
                     FontWeight="SemiBold" Foreground="White" />
          <StackPanel Orientation="Horizontal" Spacing="12" VerticalAlignment="Center">
            <Button Command="{Binding CleanTempCommand}"
                    Classes="PrimaryButton" Content="🗑 Очистить Temp" />
            <TextBlock Text="{Binding StatusText}" Foreground="#888"
                       VerticalAlignment="Center" />
          </StackPanel>
        </StackPanel>
      </Border>

      <StackPanel Orientation="Horizontal" Spacing="8">
        <TextBlock Text="Автозагрузка" FontSize="16" FontWeight="SemiBold"
                   Foreground="White" VerticalAlignment="Center" />
        <Button Command="{Binding RefreshCommand}" Content="↺" />
      </StackPanel>
    </StackPanel>

    <DataGrid ItemsSource="{Binding AutorunEntries}"
              AutoGenerateColumns="False"
              IsReadOnly="True"
              Classes="DarkGrid">
      <DataGrid.Columns>
        <DataGridTextColumn Header="Имя" Binding="{Binding Name}" Width="160" />
        <DataGridTextColumn Header="Путь" Binding="{Binding ImagePath}" Width="*" />
        <DataGridTextColumn Header="Место" Binding="{Binding Location}" Width="140" />
        <DataGridCheckBoxColumn Header="Подпись" Binding="{Binding IsSigned}" Width="70" />
        <DataGridTextColumn Header="Риск" Binding="{Binding RiskScore}" Width="60" />
      </DataGrid.Columns>
    </DataGrid>

  </DockPanel>
</UserControl>
```

---

## Task 9: SettingsView + SettingsViewModel

### SettingsViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private bool _realTimeEnabled;
    [ObservableProperty] private string _statusText = "";

    public string AppVersion => "Ninja Security v1.0.0";

    public SettingsViewModel(IpcClient ipc)
    {
        _ipc = ipc;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var response = await _ipc.SendAsync("GetRealTimeStatus");
        if (response.Success && response.Data.HasValue)
            RealTimeEnabled = response.Data.Value.GetProperty("enabled").GetBoolean();
    }

    [RelayCommand]
    private async Task ToggleRealTime()
    {
        var newState = !RealTimeEnabled;
        var response = await _ipc.SendAsync("SetRealTimeEnabled", new { Enabled = newState });
        if (response.Success) RealTimeEnabled = newState;
    }

    [RelayCommand]
    private async Task UpdateSignatures()
    {
        StatusText = "Обновление баз...";
        var response = await _ipc.SendAsync("UpdateSignatures");
        StatusText = response.Success ? "Базы обновлены ✓" : $"Ошибка: {response.Error}";
    }
}
```

### SettingsView.axaml

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:NinjaSecurity.App.ViewModels"
             x:Class="NinjaSecurity.App.Views.SettingsView"
             x:DataType="vm:SettingsViewModel">
  <ScrollViewer>
    <StackPanel Margin="32" Spacing="20">
      <TextBlock Text="Настройки" FontSize="24" FontWeight="Bold" Foreground="White" />

      <!-- Real-time protection -->
      <Border Classes="Card" Padding="20">
        <StackPanel Spacing="10">
          <TextBlock Text="Защита в реальном времени" FontSize="14"
                     FontWeight="SemiBold" Foreground="White" />
          <ToggleButton IsChecked="{Binding RealTimeEnabled}"
                        Command="{Binding ToggleRealTimeCommand}"
                        Classes="RedToggle"
                        Content="{Binding RealTimeEnabled, Converter={StaticResource BoolToStringConverter},
                                  ConverterParameter='Включена|Выключена'}" />
        </StackPanel>
      </Border>

      <!-- Signature updates -->
      <Border Classes="Card" Padding="20">
        <StackPanel Spacing="10">
          <TextBlock Text="Обновление баз сигнатур" FontSize="14"
                     FontWeight="SemiBold" Foreground="White" />
          <StackPanel Orientation="Horizontal" Spacing="12">
            <Button Command="{Binding UpdateSignaturesCommand}"
                    Classes="PrimaryButton" Content="⬇ Обновить сигнатуры" />
            <TextBlock Text="{Binding StatusText}" Foreground="#888"
                       VerticalAlignment="Center" />
          </StackPanel>
        </StackPanel>
      </Border>

      <!-- About -->
      <Border Classes="Card" Padding="20">
        <StackPanel Spacing="4">
          <TextBlock Text="О программе" FontSize="14"
                     FontWeight="SemiBold" Foreground="White" />
          <TextBlock Text="{Binding AppVersion}" Foreground="#888" />
          <TextBlock Text="ClamAV + YARA + MalSearcher" Foreground="#666" FontSize="11" />
        </StackPanel>
      </Border>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

---

## Task 10: NinjaTheme + ContentControl → View mapping + финальная сборка

### NinjaTheme.axaml

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

  <!-- Card -->
  <Style Selector="Border.Card">
    <Setter Property="Background" Value="#222222" />
    <Setter Property="CornerRadius" Value="8" />
    <Setter Property="Padding" Value="20" />
  </Style>

  <!-- StatCard -->
  <Style Selector="Border.StatCard">
    <Setter Property="Background" Value="#1E1E1E" />
    <Setter Property="CornerRadius" Value="8" />
    <Setter Property="Padding" Value="20,16" />
  </Style>

  <!-- NavButton -->
  <Style Selector="Button.NavButton">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="Foreground" Value="#AAAAAA" />
    <Setter Property="Padding" Value="12,10" />
    <Setter Property="HorizontalAlignment" Value="Stretch" />
    <Setter Property="HorizontalContentAlignment" Value="Left" />
    <Setter Property="CornerRadius" Value="6" />
    <Setter Property="FontSize" Value="14" />
  </Style>
  <Style Selector="Button.NavButton:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#252525" />
  </Style>
  <Style Selector="Button.NavButton.Active /template/ ContentPresenter">
    <Setter Property="Background" Value="#2A0808" />
    <Setter Property="TextElement.Foreground" Value="#FF4444" />
  </Style>

  <!-- PrimaryButton -->
  <Style Selector="Button.PrimaryButton">
    <Setter Property="Background" Value="#CC2222" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="Padding" Value="20,10" />
    <Setter Property="CornerRadius" Value="6" />
    <Setter Property="FontWeight" Value="SemiBold" />
    <Setter Property="FontSize" Value="14" />
  </Style>
  <Style Selector="Button.PrimaryButton:pointerover /template/ ContentPresenter">
    <Setter Property="Background" Value="#AA1818" />
  </Style>

  <!-- DangerButton -->
  <Style Selector="Button.DangerButton">
    <Setter Property="Background" Value="#441111" />
    <Setter Property="Foreground" Value="#FF4444" />
    <Setter Property="Padding" Value="12,8" />
    <Setter Property="CornerRadius" Value="6" />
  </Style>

  <!-- RedToggle -->
  <Style Selector="ToggleButton.RedToggle">
    <Setter Property="Background" Value="#333" />
    <Setter Property="Foreground" Value="#888" />
    <Setter Property="Padding" Value="16,8" />
    <Setter Property="CornerRadius" Value="20" />
  </Style>
  <Style Selector="ToggleButton.RedToggle:checked /template/ ContentPresenter">
    <Setter Property="Background" Value="#CC2222" />
    <Setter Property="TextElement.Foreground" Value="White" />
  </Style>

  <!-- DarkInput -->
  <Style Selector="TextBox.DarkInput">
    <Setter Property="Background" Value="#1E1E1E" />
    <Setter Property="Foreground" Value="White" />
    <Setter Property="BorderBrush" Value="#333" />
    <Setter Property="Padding" Value="10,6" />
    <Setter Property="CornerRadius" Value="6" />
  </Style>

  <!-- DarkGrid -->
  <Style Selector="DataGrid.DarkGrid">
    <Setter Property="Background" Value="#1A1A1A" />
    <Setter Property="RowBackground" Value="#1E1E1E" />
    <Setter Property="AlternatingRowBackground" Value="#222222" />
    <Setter Property="GridLinesVisibility" Value="None" />
    <Setter Property="Foreground" Value="#CCCCCC" />
  </Style>

</Styles>
```

### DataTemplates для ContentControl

В `App.axaml` или в `MainWindow.axaml` добавить DataTemplates:

```xml
<Window.DataTemplates>
  <DataTemplate DataType="vm:DashboardViewModel">
    <views:DashboardView />
  </DataTemplate>
  <DataTemplate DataType="vm:ScanViewModel">
    <views:ScanView />
  </DataTemplate>
  <DataTemplate DataType="vm:QuarantineViewModel">
    <views:QuarantineView />
  </DataTemplate>
  <DataTemplate DataType="vm:ProcessViewModel">
    <views:ProcessView />
  </DataTemplate>
  <DataTemplate DataType="vm:OptimizerViewModel">
    <views:OptimizerView />
  </DataTemplate>
  <DataTemplate DataType="vm:SettingsViewModel">
    <views:SettingsView />
  </DataTemplate>
</Window.DataTemplates>
```

### EqualityConverter

Создать `Converters/EqualityConverter.cs`:

```csharp
using Avalonia.Data.Converters;

namespace NinjaSecurity.App.Converters;

public class EqualityConverter : IValueConverter
{
    public static readonly EqualityConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        value?.ToString() == parameter?.ToString();

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotImplementedException();
}
```

Создать `Converters/BoolToStringConverter.cs`:

```csharp
using Avalonia.Data.Converters;

namespace NinjaSecurity.App.Converters;

public class BoolToStringConverter : IValueConverter
{
    public static readonly BoolToStringConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        var parts = parameter?.ToString()?.Split('|') ?? ["Yes", "No"];
        return (value is true) ? parts[0] : (parts.Length > 1 ? parts[1] : "");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) =>
        throw new NotImplementedException();
}
```

Зарегистрировать в `App.axaml`:
```xml
<Application.Resources>
  <ResourceDictionary>
    <conv:EqualityConverter x:Key="EqConverter" />
    <conv:BoolToStringConverter x:Key="BoolToStringConverter" />
  </ResourceDictionary>
</Application.Resources>
```

### Финальная сборка

```bash
dotnet build AppName.sln -v q
dotnet test tests/AppName.Service.Tests -v minimal
```

Ожидаем: `Build succeeded`, 80 тестов.

### Финальный коммит

```bash
git add src/NinjaSecurity.App/
git commit -m "feat: complete Avalonia GUI with all views, MVVM, Ninja theme"
```

---

## Итог Plan 3

После всех задач:
- `NinjaSecurity.App` собирается
- Все 6 экранов реализованы (Dashboard, Scan, Quarantine, Processes, Optimizer, Settings)
- Тёмная тема с красными акцентами
- IpcClient подключается к сервису через Named Pipes
- 80 тестов сервиса всё ещё проходят

**Plan 4 (будущее):**
- TrayIcon с уведомлениями о найденных угрозах
- Всплывающие уведомления при real-time обнаружении
- Анимации и polish UI
- Auto-update механизм (проверка новой версии)
- Установщик (WiX / NSIS)
