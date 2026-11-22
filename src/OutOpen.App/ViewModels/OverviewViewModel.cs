using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using OutOpen.Core.Hosting;
using OutOpen.Core.Models;

namespace OutOpen.App.ViewModels;

public sealed class OverviewViewModel : ObservableObject
{
    private const int MaxRecent = 6;

    private readonly OutOpenHost _host;

    private string _statusHeadline = string.Empty;
    private string _statusDetail = string.Empty;
    private string _sourceSummary = string.Empty;
    private string _eventSummary = string.Empty;
    private string _pauseButtonText = "暂停接管";

    public OverviewViewModel(OutOpenHost host)
    {
        _host = host;
        TogglePauseCommand = new RelayCommand(TogglePause);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        OpenDataFolderCommand = new RelayCommand(() => OpenFolder(_host.DataDirectory));
        OpenFileCommand = new RelayCommand(OpenFile);
        Refresh();
    }

    public ObservableCollection<EventRow> Recent { get; } = [];

    public RelayCommand TogglePauseCommand { get; }

    public RelayCommand OpenLogsCommand { get; }

    public RelayCommand OpenDataFolderCommand { get; }

    public RelayCommand OpenFileCommand { get; }

    public string StatusHeadline
    {
        get => _statusHeadline;
        private set => Set(ref _statusHeadline, value);
    }

    public string StatusDetail
    {
        get => _statusDetail;
        private set => Set(ref _statusDetail, value);
    }

    public string SourceSummary
    {
        get => _sourceSummary;
        private set => Set(ref _sourceSummary, value);
    }

    public string EventSummary
    {
        get => _eventSummary;
        private set => Set(ref _eventSummary, value);
    }

    public string PauseButtonText
    {
        get => _pauseButtonText;
        private set => Set(ref _pauseButtonText, value);
    }

    public void Refresh()
    {
        var paused = _host.Config.Paused;
        StatusHeadline = paused ? "已暂停接管" : "正在接管文件打开";
        StatusDetail = paused
            ? "监听仍在运行，但不会打开任何文件。点击「恢复接管」继续。"
            : "来自已启用来源应用的新文件，会在确认下载完成后交给系统默认程序打开。";
        PauseButtonText = paused ? "恢复接管" : "暂停接管";

        var enabled = _host.GetSources().Count(s => s.Enabled);
        var total = _host.GetSources().Count;
        SourceSummary = $"{enabled} / {total} 个来源应用已启用";
        EventSummary = $"{_host.RecentResults.Count} 条处理记录";

        Recent.Clear();
        foreach (var result in _host.RecentResults.Take(MaxRecent))
        {
            Recent.Add(EventRow.From(result));
        }
    }

    public void OnResult(InterceptResult result)
    {
        Recent.Insert(0, EventRow.From(result));
        while (Recent.Count > MaxRecent)
        {
            Recent.RemoveAt(Recent.Count - 1);
        }

        EventSummary = $"{_host.RecentResults.Count} 条处理记录";
    }

    private void TogglePause()
    {
        _host.SetPaused(!_host.Config.Paused);
        Refresh();
    }

    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择要交给 OutOpen 打开的文件",
            Filter = "常见文档|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx;*.txt;*.md;*.jpg;*.jpeg;*.png;*.zip|所有文件|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var result = _host.HandleManual(dialog.FileName, OpenTrigger.Manual);
        if (result.Status is not (InterceptStatus.Opened or InterceptStatus.OpenedWithFallback))
        {
            MessageBox.Show(result.Message, "OutOpen", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        Refresh();
    }

    private void OpenLogs() => OpenFolder(_host.LogDirectory);

    private static void OpenFolder(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"无法打开目录：{ex.Message}", "OutOpen", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
