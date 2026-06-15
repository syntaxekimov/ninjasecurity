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

    [ObservableProperty]
    private bool _serviceConnected;

    [ObservableProperty]
    private string _serviceStatusText = "Проверка...";

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
        _ = MonitorServiceAsync();
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
}
