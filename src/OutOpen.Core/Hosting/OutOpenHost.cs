using System.IO;
using OutOpen.Core.Adapters;
using OutOpen.Core.Catalog;
using OutOpen.Core.Config;
using OutOpen.Core.Detection;
using OutOpen.Core.Diagnostics;
using OutOpen.Core.Launching;
using OutOpen.Core.Models;
using OutOpen.Core.Pipeline;
using OutOpen.Core.Rules;
using OutOpen.Core.Viewers;

namespace OutOpen.Core.Hosting;

/// <summary>来源应用运行状态，供界面展示。</summary>
public sealed record SourceStatus(
    string Id,
    string DisplayName,
    string Priority,
    bool Enabled,
    AdapterState State,
    IReadOnlyList<string> Folders,
    string? Note);

/// <summary>OutOpen 的组合根：配置、推荐库、适配器、处理链集中在这里，WPF 界面与 CLI 共用。</summary>
public sealed class OutOpenHost : IDisposable
{
    private readonly List<FolderWatchAdapter> _adapters = [];
    private readonly List<InterceptResult> _recentResults = [];
    private readonly object _gate = new();
    private readonly IAppLog _log;
    private readonly IFileLauncher _launcher;
    private readonly IDefaultProgramResolver _resolver;
    private readonly bool _ownsLog;
    private bool _running;
    private bool _disposed;

    public OutOpenHost(
        string? dataDirectory = null,
        IAppLog? log = null,
        IFileLauncher? launcher = null,
        IDefaultProgramResolver? resolver = null,
        ViewerCatalog? catalog = null)
    {
        DataDirectory = dataDirectory ?? DefaultDataDirectory();
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);

        _ownsLog = log is null;
        _log = log ?? new FileAppLog(LogDirectory);

        ConfigStore = new ConfigStore(DataDirectory);
        Config = ConfigStore.LoadOrCreate();

        var catalogStore = new ViewerCatalogStore(DataDirectory);
        Catalog = catalog ?? catalogStore.LoadOrCreate();
        if (catalog is null)
        {
            catalogStore.Save(Catalog);
        }

        _launcher = launcher ?? new ShellLauncher();
        _resolver = resolver ?? new WindowsDefaultProgramResolver();

        Detector = new FileTypeDetector();
        Validator = new FileValidationService(Detector);
        Rules = new RulesEngine();
        ViewerAdvisor = new ViewerAdvisor(_resolver, Catalog);
        Pipeline = new InterceptPipeline(
            Detector,
            Validator,
            Rules,
            new DuplicateDetector(Config.Settings.DedupeWindowSeconds),
            _launcher,
            ViewerAdvisor,
            _log);
    }

    public string DataDirectory { get; }

    public string LogDirectory => Path.Combine(DataDirectory, "logs");

    public ConfigStore ConfigStore { get; }

    public OutOpenConfig Config { get; private set; }

    public ViewerCatalog Catalog { get; }

    public FileTypeDetector Detector { get; }

    public FileValidationService Validator { get; }

    public RulesEngine Rules { get; }

    public ViewerAdvisor ViewerAdvisor { get; }

    public InterceptPipeline Pipeline { get; }

    public IAppLog Log => _log;

    public bool IsRunning => _running;

    /// <summary>每次处理完成后触发（可能在后台线程）。</summary>
    public event EventHandler<InterceptResult>? ResultProduced;

    /// <summary>运行状态（启动/暂停/来源状态）发生变化时触发。</summary>
    public event EventHandler? StateChanged;

    public IReadOnlyList<InterceptResult> RecentResults
    {
        get
        {
            lock (_gate)
            {
                return _recentResults.ToList();
            }
        }
    }

    public static string DefaultDataDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OutOpen");

    public void Start()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(OutOpenHost));
            }

            if (_running)
            {
                return;
            }
        }

        Config = ConfigStore.LoadOrCreate();

        DisposeAdapters();

        foreach (var app in SourceApps.All)
        {
            var settings = Config.Sources.TryGetValue(app.Id, out var stored) ? stored : null;
            if (settings is { Enabled: false })
            {
                continue;
            }

            var adapter = new FolderWatchAdapter(
                app,
                settings,
                Validator,
                Config.Settings.StabilitySeconds,
                _log);

            adapter.FileArrived += OnFileArrived;
            adapter.Start();

            if (Config.Paused)
            {
                adapter.Pause();
            }

            _adapters.Add(adapter);
        }

        _running = true;
        _log.Info($"OutOpen 已启动，监听 {_adapters.Count(a => a.State == AdapterState.Running)} 个来源应用", "host");

        if (Config.Settings.ScanExistingFilesOnStart)
        {
            ScanRecentFiles();
        }

        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Stop()
    {
        DisposeAdapters();
        if (_running)
        {
            _log.Info("OutOpen 已停止监听", "host");
        }

        _running = false;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetPaused(bool paused)
    {
        Config.Paused = paused;
        foreach (var adapter in _adapters)
        {
            if (paused)
            {
                adapter.Pause();
            }
            else
            {
                adapter.Resume();
            }
        }

        ConfigStore.Save(Config);
        _log.Info(paused ? "已暂停接管" : "已恢复接管", "host");
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveConfig()
    {
        ConfigStore.Save(Config);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>重新加载配置（界面修改了来源/扩展名后调用）。</summary>
    public void ReloadFromDisk()
    {
        Config = ConfigStore.LoadOrCreate();
        if (_running)
        {
            Stop();
            Start();
        }
        else
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>手动处理一个文件（拖拽、界面按钮、命令行）。</summary>
    public InterceptResult HandleManual(string filePath, OpenTrigger trigger = OpenTrigger.Manual)
    {
        var request = new OpenRequest
        {
            FilePath = filePath,
            SourceAppId = SourceApps.Manual.Id,
            SourceDisplayName = SourceApps.Manual.DisplayName,
            Trigger = trigger,
            DetectedAt = DateTimeOffset.Now
        };

        return Process(request);
    }

    public IReadOnlyList<SourceStatus> GetSources()
    {
        var list = new List<SourceStatus>();

        foreach (var app in SourceApps.All)
        {
            var settings = Config.Sources.TryGetValue(app.Id, out var stored) ? stored : null;
            var adapter = _adapters.FirstOrDefault(a => a.App.Id == app.Id);

            list.Add(new SourceStatus(
                app.Id,
                app.DisplayName,
                app.Priority,
                settings?.Enabled ?? app.EnabledByDefault,
                adapter?.State ?? AdapterState.Stopped,
                adapter?.WatchedFolders ?? FolderResolver.ResolveFolders(app, settings),
                app.Notes));
        }

        return list;
    }

    private void OnFileArrived(object? sender, OpenRequest request) => Process(request);

    private InterceptResult Process(OpenRequest request)
    {
        InterceptResult result;
        try
        {
            result = Pipeline.Handle(request, Config);
        }
        catch (Exception ex)
        {
            _log.Error("处理请求时出现异常：" + ex.Message, "host");
            result = new InterceptResult
            {
                Status = InterceptStatus.Error,
                Request = request,
                CompletedAt = DateTimeOffset.Now,
                Message = ex.Message
            };
        }

        lock (_gate)
        {
            _recentResults.Insert(0, result);
            if (_recentResults.Count > 500)
            {
                _recentResults.RemoveRange(500, _recentResults.Count - 500);
            }
        }

        try
        {
            ResultProduced?.Invoke(this, result);
        }
        catch (Exception ex)
        {
            _log.Warn("结果通知处理器抛出异常：" + ex.Message, "host");
        }

        return result;
    }

    private void ScanRecentFiles()
    {
        var since = DateTime.Now.AddMinutes(-10);
        var dispatched = 0;

        foreach (var adapter in _adapters)
        {
            foreach (var folder in adapter.WatchedFolders)
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
                    {
                        if (File.GetLastWriteTime(file) < since)
                        {
                            continue;
                        }

                        if (!FolderResolver.IsCandidateFile(file, adapter.App.ExcludedSegments))
                        {
                            continue;
                        }

                        _ = Process(new OpenRequest
                        {
                            FilePath = file,
                            SourceAppId = adapter.App.Id,
                            SourceDisplayName = adapter.App.DisplayName,
                            Trigger = OpenTrigger.StartupScan,
                            DetectedAt = DateTimeOffset.Now
                        });

                        if (++dispatched >= 20)
                        {
                            return;
                        }
                    }
                }
                catch (Exception)
                {
                    // 单个目录不可读时跳过。
                }
            }
        }
    }

    private void DisposeAdapters()
    {
        foreach (var adapter in _adapters)
        {
            try
            {
                adapter.FileArrived -= OnFileArrived;
                adapter.Dispose();
            }
            catch (Exception)
            {
            }
        }

        _adapters.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
        if (_ownsLog && _log is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
