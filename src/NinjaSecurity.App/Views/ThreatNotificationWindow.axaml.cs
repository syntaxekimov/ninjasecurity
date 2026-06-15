using Avalonia.Controls;
using NinjaSecurity.App.ViewModels;

namespace NinjaSecurity.App.Views;

public partial class ThreatNotificationWindow : Window
{
    public ThreatNotificationWindow(string filePath, string? threatName)
    {
        InitializeComponent();
        DataContext = new ThreatNotificationViewModel
        {
            FilePath = filePath,
            ThreatName = threatName ?? "Угроза обнаружена"
        };
    }

    public async Task ShowAndAutoCloseAsync()
    {
        var screen = Screens.Primary;
        if (screen is not null)
        {
            var area = screen.WorkingArea;
            var s = screen.Scaling;
            Position = new Avalonia.PixelPoint(
                area.Right - (int)(340 * s) - 16,
                area.Bottom - (int)(90 * s) - 16);
        }
        Show();
        await Task.Delay(5000);
        Close();
    }
}
