using CommunityToolkit.Mvvm.ComponentModel;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class ProcessViewModel : ObservableObject
{
    private readonly IpcClient _ipc;
    public ProcessViewModel(IpcClient ipc) { _ipc = ipc; }
}
