using System.Windows.Threading;
using OutOpen.Core.Hosting;
using OutOpen.Core.Models;

namespace OutOpen.App.ViewModels;

public sealed record NavItem(string Title, string Icon, string Subtitle, ObservableObject ViewModel);

public sealed class MainViewModel : ObservableObject
{
    private readonly OutOpenHost _host;
    private readonly Dispatcher _dispatcher;

    private NavItem _selectedPage;
    private ObservableObject _currentPage;
    private string _currentPageTitle = string.Empty;
    private string _currentPageSubtitle = string.Empty;
    private string _statusLabel = string.Empty;
    private string _statusBrush = "#94A3B8";
    private string _appStatusText = string.Empty;

    public MainViewModel(OutOpenHost host, Dispatcher dispatcher)
    {
        _host = host;
        _dispatcher = dispatcher;

        Overview = new OverviewViewModel(host);
        Sources = new SourcesViewModel(host);
        Rules = new RulesViewModel(host);
        Viewers = new ViewersViewModel(host);
        Events = new EventsViewModel(host);
        Settings = new SettingsViewModel(host);

        Pages =
        [
            new NavItem("概览", "🏠", "运行状态与最近的事件", Overview),
            new NavItem("来源应用", "📡", "选择允许 OutOpen 接管的应用", Sources),
            new NavItem("文件规则", "📄", "按文件类型设置打开方式", Rules),
            new NavItem("查看器推荐", "🧭", "缺少查看器时给出的官方入口", Viewers),
            new NavItem("事件日志", "🕘", "最近的打开与跳过记录", Events),
            new NavItem("设置", "⚙", "启动、稳定性与日志选项", Settings)
        ];

        _selectedPage = Pages[0];
        _currentPage = Overview;
        _currentPageTitle = _selectedPage.Title;
        _currentPageSubtitle = _selectedPage.Subtitle;

        host.ResultProduced += OnResultProduced;
        host.StateChanged += OnStateChanged;
        UpdateStatus();
    }

    public OverviewViewModel Overview { get; }

    public SourcesViewModel Sources { get; }

    public RulesViewModel Rules { get; }

    public ViewersViewModel Viewers { get; }

    public EventsViewModel Events { get; }

    public SettingsViewModel Settings { get; }

    public IReadOnlyList<NavItem> Pages { get; }

    public NavItem SelectedPage
    {
        get => _selectedPage;
        set
        {
            if (!Set(ref _selectedPage, value))
            {
                return;
            }

            CurrentPage = value.ViewModel;
            CurrentPageTitle = value.Title;
            CurrentPageSubtitle = value.Subtitle;
        }
    }

    public ObservableObject CurrentPage
    {
        get => _currentPage;
        private set => Set(ref _currentPage, value);
    }

    public string CurrentPageTitle
    {
        get => _currentPageTitle;
        private set => Set(ref _currentPageTitle, value);
    }

    public string CurrentPageSubtitle
    {
        get => _currentPageSubtitle;
        private set => Set(ref _currentPageSubtitle, value);
    }

    public string StatusLabel
    {
        get => _statusLabel;
        private set => Set(ref _statusLabel, value);
    }

    public string StatusBrush
    {
        get => _statusBrush;
        private set => Set(ref _statusBrush, value);
    }

    public string AppStatusText
    {
        get => _appStatusText;
        private set => Set(ref _appStatusText, value);
    }

    public void TogglePause()
    {
        _host.SetPaused(!_host.Config.Paused);
        Overview.Refresh();
    }

    public void RefreshOverview()
    {
        Overview.Refresh();
        UpdateStatus();
    }

    private void OnResultProduced(object? sender, InterceptResult result)
    {
        _dispatcher.BeginInvoke(new Action(() =>
        {
            Overview.OnResult(result);
            Events.Append(result);
            UpdateStatus();
        }));
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvoke(new Action(() =>
        {
            Overview.Refresh();
            UpdateStatus();
        }));
    }

    private void UpdateStatus()
    {
        var paused = _host.Config.Paused;
        StatusLabel = paused ? "已暂停" : _host.IsRunning ? "正在接管" : "未启动";
        StatusBrush = paused ? "#D97706" : _host.IsRunning ? "#16A34A" : "#94A3B8";
        AppStatusText = paused
            ? "已暂停 · 后台待命"
            : _host.IsRunning
                ? "正在运行 · 后台监听"
                : "未启动";
    }
}
