using CommunityToolkit.Mvvm.ComponentModel;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IpcClient _ipc;
    public DashboardViewModel(IpcClient ipc) { _ipc = ipc; }
}
