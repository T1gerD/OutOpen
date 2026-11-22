using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using OutOpen.App.ViewModels;
using OutOpen.Core.Hosting;

namespace OutOpen.App;

/// <summary>系统托盘图标与右键菜单。</summary>
public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly MainWindow _window;
    private readonly MainViewModel _viewModel;
    private readonly OutOpenHost _host;
    private Icon? _icon;
    private bool _disposed;

    public TrayIcon(MainViewModel viewModel, OutOpenHost host, MainWindow window, bool showBalloon)
    {
        _viewModel = viewModel;
        _host = host;
        _window = window;

        _icon = LoadIcon();
        _pauseItem = new ToolStripMenuItem("暂停接管", null, (_, _) => TogglePause());

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("打开 OutOpen", null, (_, _) => ShowWindow()));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new ToolStripMenuItem("打开日志目录", null, (_, _) => OpenPath(host.LogDirectory)));
        menu.Items.Add(new ToolStripMenuItem("打开数据目录", null, (_, _) => OpenPath(host.DataDirectory)));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出 OutOpen", null, (_, _) => Exit()));

        _notifyIcon = new NotifyIcon
        {
            Icon = _icon ?? SystemIcons.Application,
            Text = "OutOpen — Open Files Your Way.",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => ShowWindow();

        window.HiddenToTray += (_, _) => ShowBalloon("OutOpen 已最小化到托盘", "双击托盘图标可以重新打开窗口。");
        host.StateChanged += (_, _) => UpdateMenu();

        UpdateMenu();

        if (showBalloon && host.Config.Settings.ShowNotifications)
        {
            ShowBalloon("OutOpen 正在后台运行", "文件落盘后会自动用外部程序打开；右键托盘图标可暂停或退出。");
        }
    }

    private void ShowWindow()
    {
        _window.ShowFromTray();
        _viewModel.RefreshOverview();
    }

    private void TogglePause()
    {
        _viewModel.TogglePause();
        UpdateMenu();
    }

    private void UpdateMenu()
    {
        void Apply()
        {
            _pauseItem.Text = _host.Config.Paused ? "恢复接管" : "暂停接管";
        }

        if (_window.Dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            _window.Dispatcher.BeginInvoke(new Action(Apply));
        }
    }

    private void Exit()
    {
        _window.Dispatcher.BeginInvoke(new Action(() =>
        {
            _window.ForceClose();
            System.Windows.Application.Current.Shutdown();
        }));
    }

    private void ShowBalloon(string title, string text)
    {
        if (!_host.Config.Settings.ShowNotifications)
        {
            return;
        }

        try
        {
            _notifyIcon.ShowBalloonTip(4000, title, text, ToolTipIcon.Info);
        }
        catch (Exception)
        {
            // 通知失败不影响主流程。
        }
    }

    private static void OpenPath(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }

    private static Icon? LoadIcon()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe))
            {
                var icon = Icon.ExtractAssociatedIcon(exe);
                if (icon is not null)
                {
                    return icon;
                }
            }
        }
        catch (Exception)
        {
        }

        return null;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon?.Dispose();
    }
}
