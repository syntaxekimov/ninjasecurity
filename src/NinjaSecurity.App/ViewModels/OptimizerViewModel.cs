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
