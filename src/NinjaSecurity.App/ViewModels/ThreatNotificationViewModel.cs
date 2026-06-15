using CommunityToolkit.Mvvm.ComponentModel;

namespace NinjaSecurity.App.ViewModels;

public partial class ThreatNotificationViewModel : ObservableObject
{
    [ObservableProperty] private string _filePath = "";
    [ObservableProperty] private string _threatName = "Угроза обнаружена";
}
