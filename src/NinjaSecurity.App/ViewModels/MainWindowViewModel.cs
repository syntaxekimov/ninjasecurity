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
}
