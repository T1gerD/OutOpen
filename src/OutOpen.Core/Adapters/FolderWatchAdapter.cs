using System.Collections.Concurrent;
using System.IO;
using OutOpen.Core.Catalog;
using OutOpen.Core.Config;
using OutOpen.Core.Detection;
using OutOpen.Core.Diagnostics;
using OutOpen.Core.Models;

namespace OutOpen.Core.Adapters;

/// <summary>来源应用适配器状态。</summary>
public enum AdapterState
{
    Stopped,
    Running,
    Paused,
    Unavailable
}

/// <summary>单个来源应用的目录监听适配器：监听应用接收文件的缓存目录，文件稳定落盘后触发打开请求。</summary>
/// <remarks>
/// 该适配器不注入应用进程，也不拦截应用内部的点击行为；它识别的是"文件已保存到磁盘"这一事件。
/// 计划书中"点击级拦截"需要逐应用单独验证，本版本采用目录监听 + 手动打开的方式，并在文档中如实说明。
/// </remarks>
public sealed class FolderWatchAdapter : IDisposable
{
    private readonly SourceAppDefinition _app;
    private readonly SourceSettings? _settings;
    private readonly FileValidationService _validator;
    private readonly IAppLog? _log;
    private readonly double _stabilitySeconds;
    private readonly List<FileSystemWatcher> _watchers = [];
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();
    private bool _disposed;

    public FolderWatchAdapter(
        SourceAppDefinition app,
        SourceSettings? settings,
        FileValidationService validator,
        double stabilitySeconds = 2.0,
        IAppLog? log = null)
    {
        _app = app;
        _settings = settings;
        _validator = validator;
        _stabilitySeconds = Math.Clamp(stabilitySeconds, 0.2, 60);
        _log = log;
        WatchedFolders = [];
    }

    /// <summary>文件稳定后触发；参数为待处理的打开请求。</summary>
    public event EventHandler<OpenRequest>? FileArrived;

    public SourceAppDefinition App => _app;

    public AdapterState State { get; private set; } = AdapterState.Stopped;

    public IReadOnlyList<string> WatchedFolders { get; private set; }

    public IReadOnlyList<string> MissingFolders { get; private set; } = [];

    /// <summary>开始监听；目录不存在时标记为不可用，而不是抛异常。</summary>
    public void Start()
    {
        lock (_gate)
        {
            if (_disposed || State == AdapterState.Running)
            {
                return;
            }

            StopCore();

            var folders = FolderResolver.ResolveFolders(_app, _settings);
            WatchedFolders = folders;

            if (folders.Count == 0)
            {
                MissingFolders = ExpandTemplates(_app, _settings);
                State = AdapterState.Unavailable;
                _log?.Info($"适配器 {_app.DisplayName} 未找到可用目录，已标记为不可用", "adapter");
                return;
            }

            MissingFolders = [];

            foreach (var folder in folders)
            {
                try
                {
                    var watcher = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = true,
                        InternalBufferSize = 64 * 1024,
                        Filter = "*.*",
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    watcher.Created += OnFileEvent;
                    watcher.Renamed += OnRenamed;
                    watcher.Error += OnWatcherError;
                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    _log?.Warn($"监听目录失败 {PathMasker.MaskPath(folder)}：{ex.Message}", "adapter");
                }
            }

            State = _watchers.Count > 0 ? AdapterState.Running : AdapterState.Unavailable;
            _log?.Info($"适配器 {_app.DisplayName} 已启动，监听 {_watchers.Count} 个目录", "adapter");
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            StopCore();
            State = AdapterState.Stopped;
        }
    }

    /// <summary>暂停只改变状态，仍保持监听（事件会被忽略），恢复时不丢失目录。</summary>
    public void Pause() => State = State == AdapterState.Running ? AdapterState.Paused : State;

    public void Resume() => State = State == AdapterState.Paused ? AdapterState.Running : State;

    private void StopCore()
    {
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnFileEvent;
                watcher.Renamed -= OnRenamed;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
            }
            catch (Exception)
            {
            }
        }

        _watchers.Clear();
    }

    private void OnRenamed(object sender, RenamedEventArgs e) => QueueFile(e.FullPath);

    private void OnFileEvent(object sender, FileSystemEventArgs e) => QueueFile(e.FullPath);

    private void OnWatcherError(object sender, ErrorEventArgs e) =>
        _log?.Warn($"目录监听错误（{_app.DisplayName}）：{e.GetException().Message}", "adapter");

    private void QueueFile(string filePath)
    {
        if (State != AdapterState.Running)
        {
            return;
        }

        if (!FolderResolver.IsCandidateFile(filePath, _app.ExcludedSegments))
        {
            return;
        }

        if (!_pending.TryAdd(filePath, 0))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                // 给写入方一点时间，再等待文件大小与时间戳稳定。
                await Task.Delay(250).ConfigureAwait(false);

                var snapshot = await _validator.WaitUntilStableAsync(filePath, _stabilitySeconds).ConfigureAwait(false);
                if (snapshot is null || State != AdapterState.Running)
                {
                    _log?.Warn($"文件未稳定或已消失，放弃处理：{PathMasker.MaskPath(filePath)}", "adapter");
                    return;
                }

                var request = new OpenRequest
                {
                    FilePath = filePath,
                    SourceAppId = _app.Id,
                    SourceDisplayName = _app.DisplayName,
                    Trigger = OpenTrigger.FolderArrival,
                    DetectedAt = DateTimeOffset.Now,
                    SizeBytes = snapshot.Value.SizeBytes
                };

                FileArrived?.Invoke(this, request);
            }
            catch (Exception ex)
            {
                _log?.Warn($"适配器处理文件失败：{ex.Message}", "adapter");
            }
            finally
            {
                _pending.TryRemove(filePath, out _);
            }
        });
    }

    private static List<string> ExpandTemplates(SourceAppDefinition app, SourceSettings? settings)
    {
        var templates = new List<string>(app.WatchRoots);
        if (settings?.ExtraFolders is { Count: > 0 } extra)
        {
            templates.AddRange(extra);
        }

        return templates.Select(t => Environment.ExpandEnvironmentVariables(t.Trim())).ToList();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopCore();
            State = AdapterState.Stopped;
        }
    }
}
