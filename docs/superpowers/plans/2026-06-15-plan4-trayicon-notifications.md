# Ninja Security — Plan 4: TrayIcon, Уведомления, Статус сервиса

> **For agentic workers:** Use `superpowers:subagent-driven-development` task-by-task.

**Goal:** Трей-иконка с меню, сворачивание в трей, push-уведомления об угрозах (кастомный popup), статус соединения с сервисом в сайдбаре.

**Зависимости:** Plan 3 завершён — Avalonia app собирается, 6 экранов работают.

---

## Карта изменений

```
src/NinjaSecurity.App/
├── App.axaml                         (Task 1 — TrayIcon в AXAML)
├── App.axaml.cs                      (Task 1 — TrayIcon handlers)
├── Views/
│   ├── MainWindow.axaml.cs           (Task 2 — override Close → minimize to tray)
│   └── ThreatNotificationWindow.axaml      (Task 4)
│   └── ThreatNotificationWindow.axaml.cs   (Task 4)
├── ViewModels/
│   ├── MainWindowViewModel.cs        (Task 3 + Task 5 — IpcEventListener + ServiceStatus)
│   └── ThreatNotificationViewModel.cs      (Task 4)
└── Services/
    └── NotificationService.cs        (Task 5 — показ popup-уведомлений)
```

---

## Task 1: TrayIcon

Avalonia 11 поддерживает `TrayIcon` через `NativeMenu`. Добавляем иконку в трей с меню: «Открыть», «Быстрое сканирование», «Выход».

**Files:**
- Modify: `src/NinjaSecurity.App/App.axaml`
- Modify: `src/NinjaSecurity.App/App.axaml.cs`

### App.axaml

Добавить `<TrayIcon.Icons>` до `</Application>`:

```xml
<Application ...>
  <Application.Styles>...</Application.Styles>
  <Application.Resources>...</Application.Resources>

  <TrayIcon.Icons>
    <TrayIcons>
      <TrayIcon ToolTipText="Ninja Security"
                Clicked="TrayIcon_Clicked">
        <TrayIcon.Menu>
          <NativeMenu>
            <NativeMenuItem Header="Открыть" Click="TrayOpen_Click" />
            <NativeMenuItemSeparator />
            <NativeMenuItem Header="Быстрое сканирование" Click="TrayQuickScan_Click" />
            <NativeMenuItemSeparator />
            <NativeMenuItem Header="Выход" Click="TrayExit_Click" />
          </NativeMenu>
        </TrayIcon.Menu>
      </TrayIcon>
    </TrayIcons>
  </TrayIcon.Icons>
</Application>
```

### App.axaml.cs

```csharp
using Avalonia;
using Avalonia.Controls;
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

    private void TrayIcon_Clicked(object? sender, EventArgs e) => ShowMainWindow();

    private void TrayOpen_Click(object? sender, EventArgs e) => ShowMainWindow();

    private void TrayQuickScan_Click(object? sender, EventArgs e)
    {
        ShowMainWindow();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            desktop.MainWindow?.DataContext is MainWindowViewModel vm)
        {
            vm.NavigateCommand.Execute("Scan");
        }
    }

    private void TrayExit_Click(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private static void ShowMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var win = desktop.MainWindow;
            if (win is null) return;
            win.Show();
            win.WindowState = WindowState.Normal;
            win.Activate();
        }
    }
}
```

### Сборка и коммит

```bash
dotnet build src/NinjaSecurity.App/NinjaSecurity.App.csproj -v q
git add src/NinjaSecurity.App/
git commit -m "feat: add TrayIcon with context menu (Open, QuickScan, Exit)"
```

---

## Task 2: Сворачивание в трей при закрытии

Переопределяем `OnClosing` в `MainWindow` — вместо закрытия прячем окно.

**File:** `src/NinjaSecurity.App/Views/MainWindow.axaml.cs`

```csharp
using Avalonia.Controls;
using System.ComponentModel;

namespace NinjaSecurity.App.Views;

public partial class MainWindow : Window
{
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
```

### Коммит

```bash
git add src/NinjaSecurity.App/Views/MainWindow.axaml.cs
git commit -m "feat: minimize to tray on window close instead of exiting"
```

---

## Task 3: Статус соединения с сервисом в сайдбаре

Добавить в `MainWindowViewModel` свойство `ServiceConnected` и периодическую проверку связи. Показывать индикатор в сайдбаре.

### MainWindowViewModel.cs — добавить

```csharp
[ObservableProperty]
private bool _serviceConnected;

[ObservableProperty]
private string _serviceStatusText = "Проверка...";
```

В конструктор добавить:
```csharp
_ = MonitorServiceAsync();
```

Новый метод:
```csharp
private async Task MonitorServiceAsync()
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
    do
    {
        var response = await _ipc.SendAsync("GetStatus");
        ServiceConnected = response.Success;
        ServiceStatusText = response.Success ? "Сервис активен" : "Сервис недоступен";
    }
    while (await timer.WaitForNextTickAsync());
}
```

### MainWindow.axaml — в сайдбар добавить статус-строку

В `DockPanel` сайдбара, вниз (`DockPanel.Dock="Bottom"`):

```xml
<Border DockPanel.Dock="Bottom" Padding="12,8" Background="#0D0D0D">
  <StackPanel Orientation="Horizontal" Spacing="6">
    <Ellipse Width="8" Height="8" VerticalAlignment="Center">
      <Ellipse.Fill>
        <SolidColorBrush Color="{Binding ServiceConnected,
          Converter={StaticResource BoolToColorConverter},
          ConverterParameter='#22CC55|#CC2222'}" />
      </Ellipse.Fill>
    </Ellipse>
    <TextBlock Text="{Binding ServiceStatusText}" FontSize="11"
               Foreground="#666" VerticalAlignment="Center" />
  </StackPanel>
</Border>
```

Поскольку `BoolToColorConverter` (цвет) сложнее, упрощаем: используем два `Ellipse`, один видим при `ServiceConnected=true`, другой при `false`:

```xml
<Border DockPanel.Dock="Bottom" Padding="12,8" Background="#0D0D0D">
  <StackPanel Orientation="Horizontal" Spacing="6">
    <Ellipse Width="8" Height="8" Fill="#22CC55" VerticalAlignment="Center"
             IsVisible="{Binding ServiceConnected}" />
    <Ellipse Width="8" Height="8" Fill="#CC2222" VerticalAlignment="Center"
             IsVisible="{Binding !ServiceConnected}" />
    <TextBlock Text="{Binding ServiceStatusText}" FontSize="11"
               Foreground="#666" VerticalAlignment="Center" />
  </StackPanel>
</Border>
```

### Коммит

```bash
git add src/NinjaSecurity.App/
git commit -m "feat: add service connection status indicator in sidebar"
```

---

## Task 4: ThreatNotificationWindow (кастомный popup)

Всплывающее окно в правом нижнем углу экрана, исчезает через 5 секунд. Без рамки, тёмный фон, красная иконка ⚔.

**Files:**
- Create: `src/NinjaSecurity.App/Views/ThreatNotificationWindow.axaml`
- Create: `src/NinjaSecurity.App/Views/ThreatNotificationWindow.axaml.cs`
- Create: `src/NinjaSecurity.App/ViewModels/ThreatNotificationViewModel.cs`

### ThreatNotificationViewModel.cs

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace NinjaSecurity.App.ViewModels;

public partial class ThreatNotificationViewModel : ObservableObject
{
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string _threatName = "Угроза обнаружена";
}
```

### ThreatNotificationWindow.axaml

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:NinjaSecurity.App.ViewModels"
        x:Class="NinjaSecurity.App.Views.ThreatNotificationWindow"
        x:DataType="vm:ThreatNotificationViewModel"
        SystemDecorations="None"
        Background="Transparent"
        TransparencyLevelHint="Transparent"
        Width="340" Height="90"
        CanResize="False"
        Topmost="True"
        ShowInTaskbar="False">

  <Border Background="#1E1E1E" CornerRadius="10"
          BorderBrush="#CC2222" BorderThickness="1"
          Padding="16,12">
    <Grid ColumnDefinitions="40,*">
      <TextBlock Grid.Column="0" Text="⚔" FontSize="28"
                 Foreground="#CC2222" VerticalAlignment="Center"
                 HorizontalAlignment="Center" />
      <StackPanel Grid.Column="1" VerticalAlignment="Center" Spacing="2">
        <TextBlock Text="{Binding ThreatName}" FontWeight="Bold"
                   Foreground="White" FontSize="13" />
        <TextBlock Text="{Binding FilePath}" Foreground="#888"
                   FontSize="11" TextTrimming="CharacterEllipsis" />
      </StackPanel>
    </Grid>
  </Border>
</Window>
```

### ThreatNotificationWindow.axaml.cs

```csharp
using Avalonia.Controls;
using Avalonia.Threading;
using NinjaSecurity.App.ViewModels;

namespace NinjaSecurity.App.Views;

public partial class ThreatNotificationWindow : Window
{
    public ThreatNotificationWindow(string filePath, string? threatName)
    {
        InitializeComponent();
        DataContext = new ThreatNotificationViewModel
        {
            FilePath = filePath,
            ThreatName = threatName ?? "Угроза обнаружена"
        };
    }

    public async Task ShowAndAutoCloseAsync()
    {
        PositionToBottomRight();
        Show();
        await Task.Delay(5000);
        await Dispatcher.UIThread.InvokeAsync(Close);
    }

    private void PositionToBottomRight()
    {
        var screen = Screens.Primary;
        if (screen is null) return;
        var workArea = screen.WorkingArea;
        Position = new Avalonia.PixelPoint(
            workArea.Right - (int)Width - 16,
            workArea.Bottom - (int)Height - 16);
    }
}
```

### Коммит

```bash
git add src/NinjaSecurity.App/
git commit -m "feat: add ThreatNotificationWindow popup (auto-closes in 5s)"
```

---

## Task 5: Wire IpcEventListener → уведомления

При старте приложения запускаем `IpcEventListener`, при получении события `ThreatFound` — показываем `ThreatNotificationWindow`.

**File:** `src/NinjaSecurity.App/App.axaml.cs` — расширить:

```csharp
// В OnFrameworkInitializationCompleted после создания MainWindow:
StartEventListener();
```

```csharp
private readonly IpcEventListener _eventListener = new();

private void StartEventListener()
{
    _eventListener.EventReceived += OnServiceEvent;
    _eventListener.Start();
}

private void OnServiceEvent(object? sender, IpcEvent evt)
{
    if (evt.EventType != "ThreatFound" || evt.Data is null) return;

    var filePath = evt.Data.Value.TryGetProperty("FilePath", out var fp)
        ? fp.GetString() ?? "" : "";
    var threatName = evt.Data.Value.TryGetProperty("ThreatName", out var tn)
        ? tn.GetString() : null;

    Dispatcher.UIThread.Post(() =>
    {
        var win = new ThreatNotificationWindow(filePath, threatName);
        _ = win.ShowAndAutoCloseAsync();
    });
}
```

Добавить `using Avalonia.Threading;` и `using NinjaSecurity.App.Ipc;`.

### Коммит

```bash
git add src/NinjaSecurity.App/
git commit -m "feat: wire IpcEventListener to show threat notifications on ThreatFound events"
```

---

## Task 6: Финальная сборка и проверка

```bash
dotnet build AppName.sln -v q
dotnet test tests/AppName.Service.Tests -v minimal
```

Ожидаем: `Build succeeded, 0 Error(s)`, `80 Passed`.

Итоговый коммит если были мелкие правки:

```bash
git add -A
git commit -m "fix: Plan 4 final build fixes"
```

---

## Итог Plan 4

- TrayIcon с меню (Открыть / Быстрое сканирование / Выход)
- Сворачивание в трей вместо выхода
- Индикатор статуса сервиса в сайдбаре (зелёный/красный)
- Всплывающий popup при обнаружении угрозы (исчезает через 5 сек)
- Push-события от сервиса подключены к GUI

**Plan 5 (будущее):**
- `dotnet publish` self-contained + NSIS/WiX установщик
- Авто-обновление приложения (GitHub Releases API)
- Анимации (fade in/out уведомлений)
- Настройка расписания сканирований
- История угроз с графиком
