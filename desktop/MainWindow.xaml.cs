using System.Windows;

namespace Gaode.Station01.Desktop;

public partial class MainWindow : Window
{
    private readonly HostRuntime _runtime = new();

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => _runtime.Dispose();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _runtime.InitializeAsync(Browser);
        }
        catch (Exception ex)
        {
            Browser.Visibility = Visibility.Collapsed;
            HostError.Visibility = Visibility.Visible;
            HostErrorTitle.Text = "桌面宿主暂不可用";
            HostErrorDetails.Text = ex.Message;
        }
    }
}
