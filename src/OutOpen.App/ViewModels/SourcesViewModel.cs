using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using OutOpen.Core.Adapters;
using OutOpen.Core.Hosting;

namespace OutOpen.App.ViewModels;

public sealed class SourceRow : ObservableObject
{
    private bool _enabled;

    public SourceRow(SourceStatus status, bool enabled, IReadOnlyList<string>? extraFolders = null)
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
        ExtraFolders = new ObservableCollection<string>(extraFolders ?? []);
        ExtraFolders.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasExtraFolders));
        _enabled = enabled;
        AddFolderCommand = new RelayCommand(() => AddFolderRequested?.Invoke(this));
        RemoveFolderCommand = new RelayCommand<string>(path =>
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                ExtraFolders.Remove(path);
                RemoveFolderRequested?.Invoke(this, path);
            }
        });
        RebuildFoldersText();
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Priority { get; }

    public string Note { get; }

    public string StateText { get; }

    public string StateBrush { get; }

    public IReadOnlyList<string> Folders { get; }

    public ObservableCollection<string> ExtraFolders { get; }

    public bool HasExtraFolders => ExtraFolders.Count > 0;

    public string FoldersText { get; private set; } = string.Empty;

    public RelayCommand AddFolderCommand { get; }

    public RelayCommand<string> RemoveFolderCommand { get; }

    public event Action<SourceRow>? AddFolderRequested;

    public event Action<SourceRow, string>? RemoveFolderRequested;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public void RebuildFoldersText()
    {
        var all = Folders.Concat(ExtraFolders)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        FoldersText = all.Count == 0
            ? "未找到目录。点「添加目录」指定网盘/应用的下载位置。"
            : string.Join("    ", all.Take(2)) + (all.Count > 2 ? $" 等 {all.Count} 个目录" : string.Empty);
        OnPropertyChanged(nameof(FoldersText));
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
            var row = new SourceRow(status, settings?.Enabled ?? status.Enabled, settings?.ExtraFolders);
            row.PropertyChanged += OnRowChanged;
            row.AddFolderRequested += OnAddFolder;
            row.RemoveFolderRequested += OnRemoveFolder;
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
            settings.ExtraFolders = row.ExtraFolders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        _host.SaveConfig();
        _host.ReloadFromDisk();
        Refresh();
        Notice = "已保存。新的监听目录与接管设置已立即生效。";
    }

    private void OnAddFolder(SourceRow row)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = $"选择「{row.DisplayName}」的下载目录（例如网盘客户端里设置的下载位置）",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };

        if (dialog.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
        {
            return;
        }

        var path = dialog.SelectedPath.TrimEnd('\\');
        if (row.Folders.Contains(path, StringComparer.OrdinalIgnoreCase) ||
            row.ExtraFolders.Contains(path, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        row.ExtraFolders.Add(path);
        row.RebuildFoldersText();
        MarkDirty("已添加目录，记得点「保存并应用更改」。");
    }

    private void OnRemoveFolder(SourceRow row, string path)
    {
        row.RebuildFoldersText();
        MarkDirty("已移除目录，记得点「保存并应用更改」。");
    }

    private void MarkDirty(string message)
    {
        IsDirty = true;
        Notice = message;
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SourceRow.Enabled))
        {
            MarkDirty("有未保存的更改。");
        }
    }
}
