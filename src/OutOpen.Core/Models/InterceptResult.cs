using OutOpen.Core.Models;
using OutOpen.Core.Viewers;

namespace OutOpen.Core.Models;

/// <summary>单次接管的最终状态。</summary>
public enum InterceptStatus
{
    /// <summary>已交给外部程序打开。</summary>
    Opened,

    /// <summary>首次启动失败，已回退到系统默认方式并成功打开。</summary>
    OpenedWithFallback,

    /// <summary>用户暂停了接管。</summary>
    SkippedPaused,

    /// <summary>规则决定不接管。</summary>
    SkippedByRule,

    /// <summary>文件类型不在支持范围内。</summary>
    SkippedUnsupportedType,

    /// <summary>重复事件，已忽略。</summary>
    SkippedDuplicate,

    /// <summary>文件不可用（未下载完成、被占用、路径失效等）。</summary>
    ValidationFailed,

    /// <summary>没有可用的查看器，已给出推荐。</summary>
    ViewerMissing,

    /// <summary>外部程序启动失败，且回退也失败。</summary>
    LaunchFailed,

    /// <summary>处理过程中出现未预期异常。</summary>
    Error
}

/// <summary>一次接管的完整结果，供界面、日志与 CLI 使用。</summary>
public sealed record InterceptResult
{
    public required InterceptStatus Status { get; init; }

    public required OpenRequest Request { get; init; }

    public required DateTimeOffset CompletedAt { get; init; }

    public string Message { get; init; } = string.Empty;

    public FileTypeInfo? FileType { get; init; }

    public FileValidationReport? Validation { get; init; }

    public LaunchResult? Launch { get; init; }

    public ViewerAssessment? Viewer { get; init; }

    public string? RuleReason { get; init; }

    public string? TargetProgram { get; init; }

    public bool IsOpened => Status is InterceptStatus.Opened or InterceptStatus.OpenedWithFallback;

    public bool IsSkipped => Status is InterceptStatus.SkippedPaused
        or InterceptStatus.SkippedByRule
        or InterceptStatus.SkippedUnsupportedType
        or InterceptStatus.SkippedDuplicate;
}
