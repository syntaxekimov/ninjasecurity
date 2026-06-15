using CommunityToolkit.Mvvm.ComponentModel;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class ScanViewModel : ObservableObject
{
    private readonly IpcClient _ipc;
    public ScanViewModel(IpcClient ipc) { _ipc = ipc; }
}
