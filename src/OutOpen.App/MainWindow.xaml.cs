using System.ComponentModel;
using System.Windows;
using OutOpen.App.ViewModels;
using OutOpen.Core.Hosting;

namespace OutOpen.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly OutOpenHost _host;
    private bool _allowClose;

    public MainWindow(MainViewModel viewModel, OutOpenHost host)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _host = host;
        DataContext = viewModel;
    }

    /// <summary>窗口被收起进托盘时触发，用于提示用户。</summary>
    public event EventHandler? HiddenToTray;

    public void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose && _host.Config.Settings.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            HiddenToTray?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Application.Current.Shutdown();
    }
}
