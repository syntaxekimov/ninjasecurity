using CommunityToolkit.Mvvm.ComponentModel;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class OptimizerViewModel : ObservableObject
{
    private readonly IpcClient _ipc;
    public OptimizerViewModel(IpcClient ipc) { _ipc = ipc; }
}
