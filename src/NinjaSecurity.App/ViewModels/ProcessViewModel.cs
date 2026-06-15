using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;
using System.Collections.ObjectModel;

namespace NinjaSecurity.App.ViewModels;

public record ProcessItem(
    int Pid, string Name, string? ExecutablePath,
    bool HasValidSignature, int RiskScore);

public partial class ProcessViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private string _filterText = "";
    [ObservableProperty] private bool _showHighRiskOnly;

    private readonly List<ProcessItem> _allProcesses = [];
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
        var filtered = _allProcesses
            .Where(p => string.IsNullOrEmpty(FilterText) ||
                        p.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
            .Where(p => !ShowHighRiskOnly || p.RiskScore >= 50)
            .OrderByDescending(p => p.RiskScore);

        Processes.Clear();
        foreach (var p in filtered) Processes.Add(p);
    }

    [RelayCommand]
    private Task Load() => LoadAsync();

    private async Task LoadAsync()
    {
        var items = await _ipc.GetDataAsync<List<ProcessItem>>("GetProcessList") ?? [];
        _allProcesses.Clear();
        _allProcesses.AddRange(items);
        ApplyFilter();
    }
}
