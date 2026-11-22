using System.Diagnostics;
using OutOpen.Core.Hosting;
using OutOpen.Core.Startup;

namespace OutOpen.App.ViewModels;

public sealed class SettingsViewModel : ObservableObject
{
    private readonly OutOpenHost _host;
    private bool _startWithWindows;
    private bool _minimizeToTray;
    private bool _showNotifications;
    private bool _autoOpenOnArrival;
    private bool _requireSignatureMatch;
    private bool _scanExistingFilesOnStart;
    private double _stabilitySeconds;
    private int _dedupeWindowSeconds;
    private int _maxFileSizeMb;
    private int _logRetentionDays;
    private string _notice = string.Empty;

    public SettingsViewModel(OutOpenHost host)
    {
        _host = host;
        SaveCommand = new RelayCommand(Save);
        ReloadCommand = new RelayCommand(Load);
        OpenDataFolderCommand = new RelayCommand(OpenDataFolder);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        Load();
    }

    public RelayCommand SaveCommand { get; }

    public RelayCommand ReloadCommand { get; }

    public RelayCommand OpenDataFolderCommand { get; }

    public RelayCommand OpenLogsCommand { get; }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => Set(ref _startWithWindows, value);
    }

    public bool MinimizeToTray
    {
        get => _minimizeToTray;
        set => Set(ref _minimizeToTray, value);
    }

    public bool ShowNotifications
    {
        get => _showNotifications;
        set => Set(ref _showNotifications, value);
    }

    public bool AutoOpenOnArrival
    {
        get => _autoOpenOnArrival;
        set => Set(ref _autoOpenOnArrival, value);
    }

    public bool RequireSignatureMatch
    {
        get => _requireSignatureMatch;
        set => Set(ref _requireSignatureMatch, value);
    }

    public bool ScanExistingFilesOnStart
    {
        get => _scanExistingFilesOnStart;
        set => Set(ref _scanExistingFilesOnStart, value);
    }

    public double StabilitySeconds
    {
        get => _stabilitySeconds;
        set => Set(ref _stabilitySeconds, value);
    }

    public int DedupeWindowSeconds
    {
        get => _dedupeWindowSeconds;
        set => Set(ref _dedupeWindowSeconds, value);
    }

    public int MaxFileSizeMb
    {
        get => _maxFileSizeMb;
        set => Set(ref _maxFileSizeMb, value);
    }

    public int LogRetentionDays
    {
        get => _logRetentionDays;
        set => Set(ref _logRetentionDays, value);
    }

    public string Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    public void Load()
    {
        var settings = _host.Config.Settings;
        MinimizeToTray = settings.MinimizeToTray;
        ShowNotifications = settings.ShowNotifications;
        AutoOpenOnArrival = settings.AutoOpenOnArrival;
        RequireSignatureMatch = settings.RequireSignatureMatch;
        ScanExistingFilesOnStart = settings.ScanExistingFilesOnStart;
        StabilitySeconds = settings.StabilitySeconds;
        DedupeWindowSeconds = settings.DedupeWindowSeconds;
        MaxFileSizeMb = settings.MaxFileSizeMb;
        LogRetentionDays = settings.LogRetentionDays;
        StartWithWindows = new AutoStartManager().IsEnabled || settings.StartWithWindows;
        Notice = string.Empty;
    }

    public void Save()
    {
        var settings = _host.Config.Settings;
        settings.MinimizeToTray = MinimizeToTray;
        settings.ShowNotifications = ShowNotifications;
        settings.AutoOpenOnArrival = AutoOpenOnArrival;
        settings.RequireSignatureMatch = RequireSignatureMatch;
        settings.ScanExistingFilesOnStart = ScanExistingFilesOnStart;
        settings.StabilitySeconds = StabilitySeconds;
        settings.DedupeWindowSeconds = DedupeWindowSeconds;
        settings.MaxFileSizeMb = MaxFileSizeMb;
        settings.LogRetentionDays = LogRetentionDays;

        var registryMessage = string.Empty;
        var manager = new AutoStartManager();
        if (StartWithWindows != manager.IsEnabled)
        {
            if (manager.SetEnabled(StartWithWindows))
            {
                registryMessage = StartWithWindows ? "已注册开机自启。" : "已取消开机自启。";
            }
            else
            {
                registryMessage = "开机自启设置失败（可能被安全软件阻止），其余设置已保存。";
            }
        }

        settings.StartWithWindows = manager.IsEnabled;

        _host.SaveConfig();
        _host.ReloadFromDisk();
        Load();
        Notice = "设置已保存。" + registryMessage;
    }

    private void OpenDataFolder() => OpenFolder(_host.DataDirectory);

    private void OpenLogs() => OpenFolder(_host.LogDirectory);

    private static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception)
        {
        }
    }
}
