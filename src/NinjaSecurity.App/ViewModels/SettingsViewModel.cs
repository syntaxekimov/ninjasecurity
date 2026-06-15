using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private bool _realTimeEnabled;
    [ObservableProperty] private string _statusText = "";
    [ObservableProperty] private string _updateStatusText = "";

    // Scan schedule
    [ObservableProperty] private bool _scheduleEnabled;
    [ObservableProperty] private bool _scheduleDaily = true;
    [ObservableProperty] private bool _scheduleWeekly;
    [ObservableProperty] private bool _scheduleScanQuick = true;
    [ObservableProperty] private bool _scheduleScanFull;
    [ObservableProperty] private string _lastScanText = "Никогда";

    public string AppVersion => "Ninja Security v1.0.0";

    public SettingsViewModel(IpcClient ipc)
    {
        _ipc = ipc;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var rtResponse = await _ipc.SendAsync("GetRealTimeStatus");
        if (rtResponse.Success && rtResponse.Data.HasValue)
            RealTimeEnabled = rtResponse.Data.Value.GetProperty("enabled").GetBoolean();

        var schedResponse = await _ipc.SendAsync("GetScanSchedule");
        if (schedResponse.Success && schedResponse.Data.HasValue)
        {
            var d = schedResponse.Data.Value;
            ScheduleEnabled = d.GetProperty("Enabled").GetBoolean();
            var intervalHours = d.GetProperty("IntervalHours").GetInt32();
            ScheduleWeekly = intervalHours >= 168;
            ScheduleDaily = !ScheduleWeekly;
            var scanType = d.GetProperty("ScanType").GetString();
            ScheduleScanFull = scanType == "Full";
            ScheduleScanQuick = !ScheduleScanFull;

            if (d.TryGetProperty("LastRunUtc", out var lastRun) &&
                lastRun.ValueKind != System.Text.Json.JsonValueKind.Null &&
                DateTime.TryParse(lastRun.GetString(), out var dt))
            {
                LastScanText = dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            }
        }
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

    [RelayCommand]
    private async Task SaveSchedule()
    {
        var scanType = ScheduleScanFull ? "Full" : "Quick";
        var intervalHours = ScheduleWeekly ? 168 : 24;
        var response = await _ipc.SendAsync("SetScanSchedule", new
        {
            Enabled = ScheduleEnabled,
            ScanType = scanType,
            IntervalHours = intervalHours
        });
        StatusText = response.Success ? "Расписание сохранено ✓" : $"Ошибка: {response.Error}";
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        UpdateStatusText = "Проверка обновлений...";
        var response = await _ipc.SendAsync("CheckAppUpdate");
        if (!response.Success)
        {
            UpdateStatusText = "Ошибка проверки обновлений";
            return;
        }
        if (response.Data.HasValue)
        {
            var updateAvailable = response.Data.Value.GetProperty("UpdateAvailable").GetBoolean();
            if (updateAvailable)
            {
                var ver = response.Data.Value.GetProperty("LatestVersion").GetString();
                UpdateStatusText = $"Доступна новая версия: {ver}";
            }
            else
            {
                UpdateStatusText = "Установлена последняя версия ✓";
            }
        }
    }
}
