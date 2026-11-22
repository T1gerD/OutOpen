using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using OutOpen.App.ViewModels;
using OutOpen.Core.Diagnostics;
using OutOpen.Core.Hosting;

namespace OutOpen.App;

public partial class App : Application
{
    private const string MutexName = @"Local\OutOpen.SingleInstance";
    private const string ExitEventName = @"Local\OutOpen.ExitRequested";

    private Mutex? _mutex;
    private EventWaitHandle? _exitEvent;
    private OutOpenHost? _host;
    private MainViewModel? _viewModel;
    private MainWindow? _window;
    private TrayIcon? _tray;
    private bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // 第二个实例：带 --quit 时通知已运行的实例退出（卸载/脚本用），否则只提示。
            if (e.Args.Any(a => string.Equals(a, "--quit", StringComparison.OrdinalIgnoreCase)))
            {
                TryRequestExit();
            }
            else
            {
                MessageBox.Show(
                    "OutOpen 已经在运行了。可以在任务栏托盘里找到它。",
                    "OutOpen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }

            Shutdown();
            return;
        }

        _ownsMutex = true;
        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ExitEventName);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        try
        {
            _host = new OutOpenHost();
            _host.Start();

            _viewModel = new MainViewModel(_host, Dispatcher);
            _window = new MainWindow(_viewModel, _host);
            MainWindow = _window;

            var startHidden = e.Args.Any(a => string.Equals(a, "--tray", StringComparison.OrdinalIgnoreCase));
            if (!startHidden)
            {
                _window.Show();
            }

            _tray = new TrayIcon(_viewModel, _host, _window, showBalloon: startHidden);
            StartExitListener();
        }
        catch (Exception ex)
        {
            TryLog(ex);
            MessageBox.Show(
                "OutOpen 启动失败：" + ex.Message + "\n\n详情见日志目录。",
                "OutOpen",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();

        try
        {
            _host?.Stop();
            _host?.Dispose();
        }
        catch (Exception ex)
        {
            TryLog(ex);
        }

        if (_ownsMutex)
        {
            try
            {
                _mutex?.ReleaseMutex();
            }
            catch (ApplicationException)
            {
            }
        }

        _mutex?.Dispose();
        _exitEvent?.Dispose();
        base.OnExit(e);
    }

    /// <summary>监听 --quit 通知；在后台线程等待，收到后回到 UI 线程正常退出。</summary>
    private void StartExitListener()
    {
        if (_exitEvent is null)
        {
            return;
        }

        var listener = new Thread(() =>
        {
            try
            {
                while (_exitEvent.WaitOne())
                {
                    Dispatcher.BeginInvoke(() =>
                    {
                        _window?.ForceClose();
                        Shutdown();
                    });
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
            }
        })
        {
            IsBackground = true,
            Name = "OutOpen.ExitListener"
        };
        listener.Start();
    }

    /// <summary>作为第二个实例启动时，向已运行的实例发送退出请求。</summary>
    private static void TryRequestExit()
    {
        try
        {
            using var exitEvent = EventWaitHandle.OpenExisting(ExitEventName);
            exitEvent.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        TryLog(e.Exception);
        MessageBox.Show(
            "OutOpen 遇到一个未预期的错误，已经记录到日志：\n" + e.Exception.Message,
            "OutOpen",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            TryLog(ex);
        }
    }

    private void TryLog(Exception ex)
    {
        try
        {
            _host?.Log.Error(ex.ToString(), "app");
        }
        catch (Exception)
        {
        }
    }
}
