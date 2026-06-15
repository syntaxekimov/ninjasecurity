using CommunityToolkit.Mvvm.ComponentModel;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class QuarantineViewModel : ObservableObject
{
    private readonly IpcClient _ipc;
    public QuarantineViewModel(IpcClient ipc) { _ipc = ipc; }
}
