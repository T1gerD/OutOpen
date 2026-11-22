using System.Collections.ObjectModel;
using System.Diagnostics;
using OutOpen.Core.Catalog;
using OutOpen.Core.Hosting;
using OutOpen.Core.Viewers;

namespace OutOpen.App.ViewModels;

public sealed record ViewerExtensionOption(string Extension, string DisplayName)
{
    public string Label => $"{Extension}  {DisplayName}";
}

public sealed record ViewerRow(
    string Name,
    string Purpose,
    string OfficialUrl,
    string? DownloadUrl,
    string VerifiedAt,
    bool IsPrimary)
{
    public string LinkText => IsPrimary ? "首选推荐 · 打开官方页面" : "打开官方页面";
}

public sealed class ViewersViewModel : ObservableObject
{
    private readonly OutOpenHost _host;
    private ViewerExtensionOption _selectedExtension;
    private string _statusText = string.Empty;
    private string _statusBrush = "#16A34A";
    private string _detailText = string.Empty;
    private string _stateText = string.Empty;
    private string _notice = string.Empty;

    public ViewersViewModel(OutOpenHost host)
    {
        _host = host;
        _selectedExtension = new ViewerExtensionOption(string.Empty, string.Empty);
        Extensions = SupportedTypes.All
            .Select(t => new ViewerExtensionOption(t.Extension, t.DisplayName))
            .ToList();

        RecheckCommand = new RelayCommand(Assess);
        SnoozeCommand = new RelayCommand(Snooze);
        IgnoreCommand = new RelayCommand(Ignore);
        ResetCommand = new RelayCommand(Reset);
        OpenLinkCommand = new RelayCommand<string>(OpenLink);

        _selectedExtension = Extensions[0];
        Assess();
    }

    public IReadOnlyList<ViewerExtensionOption> Extensions { get; }

    public ObservableCollection<ViewerRow> Recommendations { get; } = [];

    public RelayCommand RecheckCommand { get; }

    public RelayCommand SnoozeCommand { get; }

    public RelayCommand IgnoreCommand { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand<string> OpenLinkCommand { get; }

    public ViewerExtensionOption SelectedExtension
    {
        get => _selectedExtension;
        set
        {
            if (Set(ref _selectedExtension, value))
            {
                Assess();
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public string StatusBrush
    {
        get => _statusBrush;
        private set => Set(ref _statusBrush, value);
    }

    public string DetailText
    {
        get => _detailText;
        private set => Set(ref _detailText, value);
    }

    public string StateText
    {
        get => _stateText;
        private set => Set(ref _stateText, value);
    }

    public string Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    public void Assess()
    {
        Recommendations.Clear();

        if (string.IsNullOrEmpty(SelectedExtension.Extension))
        {
            return;
        }

        var assessment = _host.ViewerAdvisor.Assess(_host.Config, SelectedExtension.Extension);
        var type = SupportedTypes.Find(SelectedExtension.Extension);

        StatusText = assessment.HasViewer ? "系统已有关联程序" : assessment.StatusText;
        StatusBrush = assessment.HasViewer ? "#16A34A" : "#D97706";
        DetailText = assessment.HasViewer
            ? $"默认程序：{assessment.DefaultProgramPath}"
            : "OutOpen 不会自动安装、也不会修改你的默认关联；下面是从官方渠道核验过的可选查看器。";
        if (assessment.SnoozeUntil is { } until)
        {
            StateText = $"已稍后提醒：{until.ToLocalTime():yyyy-MM-dd HH:mm} 之后再提醒";
        }
        else
        {
            StateText = assessment.Suppressed
                ? "已隐藏该类型的推荐（可点「恢复推荐」重新显示）"
                : type is null
                    ? string.Empty
                    : $"类型：{type.DisplayName} · {type.MimeType}";
        }

        foreach (var recommendation in assessment.AllRecommendations)
        {
            Recommendations.Add(new ViewerRow(
                recommendation.Name,
                recommendation.Purpose,
                recommendation.OfficialUrl,
                recommendation.DownloadUrl,
                recommendation.VerifiedAt,
                ReferenceEquals(recommendation, assessment.Primary)));
        }

        Notice = string.Empty;
    }

    private void Snooze()
    {
        _host.ViewerAdvisor.Snooze(_host.Config, SelectedExtension.Extension, TimeSpan.FromDays(7));
        _host.SaveConfig();
        Assess();
        Notice = "已推迟 7 天提醒。";
    }

    private void Ignore()
    {
        _host.ViewerAdvisor.Ignore(_host.Config, SelectedExtension.Extension);
        _host.SaveConfig();
        Assess();
        Notice = "不再提醒该类型；可以随时恢复。";
    }

    private void Reset()
    {
        _host.ViewerAdvisor.Reset(_host.Config, SelectedExtension.Extension);
        _host.SaveConfig();
        Assess();
        Notice = "推荐已恢复。";
    }

    private static void OpenLink(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception)
        {
            // 浏览器无法启动时保持界面可用。
        }
    }
}
