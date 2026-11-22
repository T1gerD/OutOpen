using System.Collections.ObjectModel;
using OutOpen.Core.Hosting;
using OutOpen.Core.Models;

namespace OutOpen.App.ViewModels;

public sealed class EventsViewModel : ObservableObject
{
    private const int MaxRows = 200;

    private readonly OutOpenHost _host;

    public EventsViewModel(OutOpenHost host)
    {
        _host = host;
        RefreshCommand = new RelayCommand(Refresh);
        ClearCommand = new RelayCommand(Clear);
        Refresh();
    }

    public ObservableCollection<EventRow> Rows { get; } = [];

    public RelayCommand RefreshCommand { get; }

    public RelayCommand ClearCommand { get; }

    public string EmptyText => Rows.Count == 0
        ? "还没有事件。把文件拖到「概览」里的「手动打开文件」，或者等待来源应用收到新文件。"
        : string.Empty;

    public void Refresh()
    {
        Rows.Clear();
        foreach (var result in _host.RecentResults.Take(MaxRows))
        {
            Rows.Add(EventRow.From(result));
        }

        OnPropertyChanged(nameof(EmptyText));
    }

    public void Append(InterceptResult result)
    {
        Rows.Insert(0, EventRow.From(result));
        while (Rows.Count > MaxRows)
        {
            Rows.RemoveAt(Rows.Count - 1);
        }

        OnPropertyChanged(nameof(EmptyText));
    }

    private void Clear()
    {
        Rows.Clear();
        OnPropertyChanged(nameof(EmptyText));
    }
}
