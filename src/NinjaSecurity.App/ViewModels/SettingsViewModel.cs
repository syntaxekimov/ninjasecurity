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
