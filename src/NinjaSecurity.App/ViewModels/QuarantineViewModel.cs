using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;
using System.Collections.ObjectModel;

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
