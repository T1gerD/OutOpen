using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using OutOpen.App.ViewModels;
using OutOpen.Core.Diagnostics;
using OutOpen.Core.Hosting;

namespace OutOpen.App;

public partial class App : Application
{
    // Windows GUI 子系统进程默认没有控制台。如果宿主（脚本、调试器、某些启动器）
    // 把本进程挂到了一个控制台上，这里主动脱离，保证关闭终端不会影响软件本体。
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

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
        // 结构性保证：GUI 进程不依附任何控制台。没有控制台时返回 false，属正常情况。
        FreeConsole();

        base.OnStartup(e);

        // 卸载程序/脚本用 --quit 请求已运行的实例退出。
        // 关键：没有实例在运行时也必须立刻退出，绝不能把卸载流程变成一次正常启动。
        if (e.Args.Any(a => string.Equals(a, "--quit", StringComparison.OrdinalIgnoreCase)))
        {
            TryRequestExit();
            Shutdown();
            return;
        }

        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "OutOpen 已经在运行了。可以在任务栏托盘里找到它。",
                "OutOpen",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

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

    /// <summary>向已运行的实例发送退出请求；实例可能还在启动中，因此短暂重试。</summary>
    private static void TryRequestExit()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var exitEvent = EventWaitHandle.OpenExisting(ExitEventName);
                exitEvent.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(100);
            }
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
