using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using NinjaSecurity.App.Ipc;
using NinjaSecurity.App.Views;
using NinjaSecurity.App.ViewModels;

namespace NinjaSecurity.App;

public partial class App : Application
{
    private readonly IpcEventListener _eventListener = new();
    private TrayIcon? _trayIcon;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            SetupTrayIcon();
            StartEventListener();
        }
        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon()
    {
        var menu = new NativeMenu();

        var openItem = new NativeMenuItem("Открыть");
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Add(openItem);

        menu.Add(new NativeMenuItemSeparator());

        var scanItem = new NativeMenuItem("Быстрое сканирование");
        scanItem.Click += (_, _) =>
        {
            ShowMainWindow();
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow.DataContext: MainWindowViewModel vm })
                vm.NavigateCommand.Execute("Scan");
        };
        menu.Add(scanItem);

        menu.Add(new NativeMenuItemSeparator());

        var exitItem = new NativeMenuItem("Выход");
        exitItem.Click += (_, _) =>
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
                d.Shutdown();
        };
        menu.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Ninja Security",
            Menu = menu,
            IsVisible = true
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow();

        TrayIcon.SetIcons(this, new TrayIcons { _trayIcon });
    }

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

    private static void ShowMainWindow()
    {
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var win = desktop.MainWindow;
            if (win is null) return;
            win.Show();
            win.WindowState = WindowState.Normal;
            win.Activate();
        }
    }
}
