using System.IO;

namespace OutOpen.Core.Models;

/// <summary>打开请求的触发方式。</summary>
public enum OpenTrigger
{
    /// <summary>来源应用的缓存目录中新落盘的文件。</summary>
    FolderArrival,

    /// <summary>用户拖拽到 OutOpen 窗口。</summary>
    DragDrop,

    /// <summary>手动打开（界面按钮 / 托盘菜单）。</summary>
    Manual,

    /// <summary>命令行调用。</summary>
    CommandLine,

    /// <summary>启动时扫描已存在的文件（默认关闭）。</summary>
    StartupScan
}

/// <summary>一次待处理的文件打开请求：来源应用 + 文件路径 + 触发方式。</summary>
public sealed record OpenRequest
{
    public required string FilePath { get; init; }

    public required string SourceAppId { get; init; }

    public string SourceDisplayName { get; init; } = string.Empty;

    public OpenTrigger Trigger { get; init; } = OpenTrigger.Manual;

    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.Now;

    public long SizeBytes { get; init; }

    public string? RequestId { get; init; } = Guid.NewGuid().ToString("N")[..12];

    /// <summary>小写扩展名，包含点号；没有扩展名时为空字符串。</summary>
    public string Extension => Path.GetExtension(FilePath).ToLowerInvariant();

    /// <summary>展开为绝对路径，失败时回退原始字符串。</summary>
    public string NormalizedPath
    {
        get
        {
            try
            {
                return Path.GetFullPath(FilePath);
            }
            catch (Exception)
            {
                return FilePath;
            }
        }
    }
}
