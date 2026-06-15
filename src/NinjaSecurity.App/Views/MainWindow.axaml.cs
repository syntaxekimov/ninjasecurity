using Avalonia.Controls;

namespace NinjaSecurity.App.Views;

public partial class MainWindow : Window
{
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }
}
