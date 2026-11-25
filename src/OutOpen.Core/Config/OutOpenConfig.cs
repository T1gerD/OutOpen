using System.Text.Json.Serialization;

namespace OutOpen.Core.Config;

/// <summary>OutOpen 的用户配置，以 JSON 保存在本地。</summary>
public sealed class OutOpenConfig
{
    public const int CurrentVersion = 2;

    public int Version { get; set; } = CurrentVersion;

    /// <summary>暂停接管（仍监听事件，但不打开文件）。</summary>
    public bool Paused { get; set; }

    public GeneralSettings Settings { get; set; } = new();

    /// <summary>来源应用设置，键为来源应用 id。</summary>
    public Dictionary<string, SourceSettings> Sources { get; set; } = new();

    /// <summary>文件类型（扩展名）规则，键为小写扩展名。</summary>
    public Dictionary<string, ExtensionSettings> Extensions { get; set; } = new();

    /// <summary>高级规则：可按来源应用 + 扩展名覆盖默认策略。</summary>
    public List<RuleSettings> Rules { get; set; } = [];

    /// <summary>查看器推荐的忽略 / 稍后提醒状态，键为扩展名。</summary>
    public Dictionary<string, RecommendationState> RecommendationStates { get; set; } = new();
}

public sealed class GeneralSettings
{
    /// <summary>文件大小与时间戳需要保持不变的秒数，防止打开未下载完成的文件。</summary>
    public double StabilitySeconds { get; set; } = 2.0;

    /// <summary>同一文件在该秒数内重复到达时只处理一次。</summary>
    public int DedupeWindowSeconds { get; set; } = 8;

    /// <summary>普通文件（文档、图片等）超过该大小不接管；0 表示不限制。</summary>
    public int MaxFileSizeMb { get; set; } = 512;

    /// <summary>压缩包（zip/7z/rar）单独的大小上限，默认 8 GB；0 表示不限制。</summary>
    public int MaxArchiveSizeMb { get; set; } = 8192;

    /// <summary>要求文件签名与扩展名一致；不一致时按失败处理。</summary>
    public bool RequireSignatureMatch { get; set; }

    /// <summary>接收完成后自动打开（总开关）。关闭后只记录事件。</summary>
    public bool AutoOpenOnArrival { get; set; } = true;

    /// <summary>随 Windows 启动。</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>关闭主窗口时最小化到系统托盘。</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>显示托盘气泡通知。</summary>
    public bool ShowNotifications { get; set; } = true;

    /// <summary>日志保留天数。</summary>
    public int LogRetentionDays { get; set; } = 14;

    /// <summary>启动时扫描监听目录中已存在的文件（默认关闭，避免打扰）。</summary>
    public bool ScanExistingFilesOnStart { get; set; }
}

public sealed class SourceSettings
{
    public bool Enabled { get; set; }

    /// <summary>额外监听的目录（绝对路径或含环境变量的路径）。</summary>
    public List<string> ExtraFolders { get; set; } = [];
}

public sealed class ExtensionSettings
{
    public bool Enabled { get; set; } = true;

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Models.OpenAction Action { get; set; } = Models.OpenAction.ExternalOpen;

    /// <summary>指定打开程序；为空时使用系统默认关联。</summary>
    public string? TargetProgram { get; set; }
}

public sealed class RuleSettings
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];

    /// <summary>为空表示匹配任意来源应用。</summary>
    public string? SourceAppId { get; set; }

    /// <summary>为空表示匹配任意扩展名。</summary>
    public List<string> Extensions { get; set; } = [];

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Models.OpenAction Action { get; set; } = Models.OpenAction.ExternalOpen;

    public string? TargetProgram { get; set; }

    /// <summary>数值越大优先级越高。</summary>
    public int Priority { get; set; }
}

public sealed class RecommendationState
{
    public bool Ignored { get; set; }

    public DateTimeOffset? SnoozeUntil { get; set; }
}
