using System.Collections.ObjectModel;
using System.ComponentModel;
using OutOpen.Core.Adapters;
using OutOpen.Core.Hosting;

namespace OutOpen.App.ViewModels;

public sealed class SourceRow : ObservableObject
{
    private bool _enabled;

    public SourceRow(SourceStatus status, bool enabled)
    {
        Id = status.Id;
        DisplayName = status.DisplayName;
        Priority = status.Priority;
        Note = status.Note ?? string.Empty;
        StateText = status.State switch
        {
            AdapterState.Running => "监听中",
            AdapterState.Paused => "已暂停",
            AdapterState.Unavailable => "未找到目录",
            _ => "未启动"
        };
        StateBrush = status.State switch
        {
            AdapterState.Running => "#16A34A",
            AdapterState.Paused => "#D97706",
            _ => "#94A3B8"
        };
        Folders = status.Folders;
        FoldersText = status.Folders.Count == 0
            ? "本机未找到该应用的接收目录（可在配置中手动添加）。"
            : string.Join("    ", status.Folders.Take(2)) + (status.Folders.Count > 2 ? $" 等 {status.Folders.Count} 个目录" : string.Empty);
        _enabled = enabled;
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Priority { get; }

    public string Note { get; }

    public string StateText { get; }

    public string StateBrush { get; }

    public IReadOnlyList<string> Folders { get; }

    public string FoldersText { get; }

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }
}

public sealed class SourcesViewModel : ObservableObject
{
    private readonly OutOpenHost _host;
    private bool _isDirty;
    private string _notice = string.Empty;

    public SourcesViewModel(OutOpenHost host)
    {
        _host = host;
        SaveCommand = new RelayCommand(Save);
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public ObservableCollection<SourceRow> Rows { get; } = [];

    public RelayCommand SaveCommand { get; }

    public RelayCommand RefreshCommand { get; }

    public bool IsDirty
    {
        get => _isDirty;
        private set
        {
            if (Set(ref _isDirty, value))
            {
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
    }

    public string SaveButtonText => IsDirty ? "保存并应用更改" : "保存并应用";

    public string Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    public void Refresh()
    {
        Rows.Clear();

        foreach (var status in _host.GetSources())
        {
            var settings = _host.Config.Sources.TryGetValue(status.Id, out var stored) ? stored : null;
            var row = new SourceRow(status, settings?.Enabled ?? status.Enabled);
            row.PropertyChanged += OnRowChanged;
            Rows.Add(row);
        }

        IsDirty = false;
        Notice = string.Empty;
    }

    public void Save()
    {
        foreach (var row in Rows)
        {
            if (!_host.Config.Sources.TryGetValue(row.Id, out var settings))
            {
                settings = new Core.Config.SourceSettings();
                _host.Config.Sources[row.Id] = settings;
            }

            settings.Enabled = row.Enabled;
        }

        _host.SaveConfig();
        _host.ReloadFromDisk();
        Refresh();
        Notice = "已保存。新的接管设置已立即生效。";
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SourceRow.Enabled))
        {
            IsDirty = true;
            Notice = "有未保存的更改。";
        }
    }
}
