using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private bool _realTimeEnabled;
    [ObservableProperty] private string _statusText = "Подключение к сервису...";
    [ObservableProperty] private bool _serviceConnected;
    [ObservableProperty] private int _threatsFound;
    [ObservableProperty] private int _quarantineCount;
    [ObservableProperty] private string _lastScanText = "—";

    public DashboardViewModel(IpcClient ipc)
    {
        _ipc = ipc;
        _ = LoadStatusAsync();
    }

    private async Task LoadStatusAsync()
    {
        var response = await _ipc.SendAsync("GetStatus");
        ServiceConnected = response.Success;

        if (!response.Success)
        {
            StatusText = "Сервис недоступен";
            return;
        }

        var rtResponse = await _ipc.SendAsync("GetRealTimeStatus");
        if (rtResponse.Success && rtResponse.Data.HasValue)
            RealTimeEnabled = rtResponse.Data.Value.GetProperty("enabled").GetBoolean();

        StatusText = RealTimeEnabled ? "Защита активна" : "Защита отключена";

        var statsResponse = await _ipc.SendAsync("GetDashboardStats");
        if (statsResponse.Success && statsResponse.Data.HasValue)
        {
            var d = statsResponse.Data.Value;
            QuarantineCount = d.GetProperty("quarantineCount").GetInt32();
            if (d.TryGetProperty("lastScanUtc", out var lastScan) &&
                lastScan.ValueKind != System.Text.Json.JsonValueKind.Null &&
                DateTime.TryParse(lastScan.GetString(), out var dt))
            {
                LastScanText = dt.ToLocalTime().ToString("dd.MM HH:mm");
            }
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
        StatusText = "Сканирование...";
        var response = await _ipc.SendAsync("StartScan", new { Type = "Quick" });
        if (response.Success && response.Data.HasValue)
        {
            ThreatsFound = response.Data.Value.GetProperty("threatsFound").GetInt32();
            StatusText = ThreatsFound > 0
                ? $"Найдено угроз: {ThreatsFound}"
                : "Угрозы не обнаружены ✓";
            LastScanText = DateTime.Now.ToString("dd.MM HH:mm");
        }
        else
        {
            StatusText = response.Error ?? "Ошибка сканирования";
        }
    }
}
