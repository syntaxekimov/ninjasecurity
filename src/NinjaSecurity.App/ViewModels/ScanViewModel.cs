using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NinjaSecurity.App.Ipc;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace NinjaSecurity.App.ViewModels;

public record ThreatResult(string FilePath, string? ThreatName, int ConfidenceScore);

public partial class ScanViewModel : ObservableObject
{
    private readonly IpcClient _ipc;

    [ObservableProperty] private bool _isQuick = true;
    [ObservableProperty] private bool _isFull;
    [ObservableProperty] private bool _isCustom;
    [ObservableProperty] private string _customPath = "";
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private string _statusText = "Готов к сканированию";

    public ObservableCollection<ThreatResult> Threats { get; } = [];

    public ScanViewModel(IpcClient ipc) => _ipc = ipc;

    [RelayCommand]
    private async Task StartScan()
    {
        IsScanning = true;
        Threats.Clear();
        StatusText = "Сканирование...";

        string scanType = IsCustom ? "Custom" : IsFull ? "Full" : "Quick";
        object payload = scanType == "Custom"
            ? new { Type = "Custom", Path = CustomPath }
            : (object)new { Type = scanType };

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
