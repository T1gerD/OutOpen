using System.Collections.ObjectModel;
using System.ComponentModel;
using OutOpen.Core.Catalog;
using OutOpen.Core.Config;
using OutOpen.Core.Hosting;
using OutOpen.Core.Models;

namespace OutOpen.App.ViewModels;

public sealed record ActionOption(OpenAction Value, string Label);

public sealed class ExtensionRow : ObservableObject
{
    private static readonly IReadOnlyList<ActionOption> ActionOptions =
    [
        new(OpenAction.ExternalOpen, "用外部程序打开"),
        new(OpenAction.SystemDefault, "用系统默认方式打开"),
        new(OpenAction.Skip, "不自动打开")
    ];

    private bool _enabled;
    private OpenAction _action;
    private string _targetProgram;

    public ExtensionRow(string extension, string displayName, string categoryText, ExtensionSettings settings)
    {
        Extension = extension;
        DisplayName = displayName;
        CategoryText = categoryText;
        _enabled = settings.Enabled;
        _action = settings.Action;
        _targetProgram = settings.TargetProgram ?? string.Empty;
    }

    public string Extension { get; }

    public string DisplayName { get; }

    public string CategoryText { get; }

    public IReadOnlyList<ActionOption> Actions => ActionOptions;

    public bool Enabled
    {
        get => _enabled;
        set => Set(ref _enabled, value);
    }

    public OpenAction Action
    {
        get => _action;
        set => Set(ref _action, value);
    }

    public string TargetProgram
    {
        get => _targetProgram;
        set => Set(ref _targetProgram, value);
    }
}

public sealed class RulesViewModel : ObservableObject
{
    private readonly OutOpenHost _host;
    private bool _isDirty;
    private string _notice = string.Empty;

    public RulesViewModel(OutOpenHost host)
    {
        _host = host;
        SaveCommand = new RelayCommand(Save);
        RefreshCommand = new RelayCommand(Refresh);
        Refresh();
    }

    public ObservableCollection<ExtensionRow> Rows { get; } = [];

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

    public string SaveButtonText => IsDirty ? "保存规则" : "保存规则";

    public string Notice
    {
        get => _notice;
        private set => Set(ref _notice, value);
    }

    public void Refresh()
    {
        Rows.Clear();

        foreach (var type in SupportedTypes.All)
        {
            var settings = ConfigStore.GetExtensionSettings(_host.Config, type.Extension);
            var row = new ExtensionRow(type.Extension, type.DisplayName, CategoryLabel(type.Category), settings);
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
            _host.Config.Extensions[row.Extension] = new ExtensionSettings
            {
                Enabled = row.Enabled,
                Action = row.Action,
                TargetProgram = string.IsNullOrWhiteSpace(row.TargetProgram) ? null : row.TargetProgram.Trim()
            };
        }

        _host.SaveConfig();
        _host.ReloadFromDisk();
        Refresh();
        Notice = "规则已保存。修改会在下一次文件到达时生效。";
    }

    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExtensionRow.Enabled) or nameof(ExtensionRow.Action) or nameof(ExtensionRow.TargetProgram))
        {
            IsDirty = true;
            Notice = "有未保存的更改。";
        }
    }

    private static string CategoryLabel(FileCategory category) => category switch
    {
        FileCategory.Document => "文档",
        FileCategory.Spreadsheet => "表格",
        FileCategory.Presentation => "演示",
        FileCategory.Text => "文本",
        FileCategory.Image => "图片",
        FileCategory.Archive => "压缩包",
        _ => "其他"
    };
}
