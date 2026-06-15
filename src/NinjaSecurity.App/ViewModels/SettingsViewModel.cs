using CommunityToolkit.Mvvm.ComponentModel;
using NinjaSecurity.App.Ipc;

namespace NinjaSecurity.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IpcClient _ipc;
    public SettingsViewModel(IpcClient ipc) { _ipc = ipc; }
}
