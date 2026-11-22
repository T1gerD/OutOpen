using System.IO;
using System.Windows.Media;
using OutOpen.Core.Models;

namespace OutOpen.App.ViewModels;

/// <summary>事件列表的一行。</summary>
public sealed record EventRow(
    string TimeText,
    string StatusText,
    Brush StatusBrush,
    string FileName,
    string TypeText,
    string Message)
{
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
    private static readonly Brush Gray = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

    public static EventRow From(InterceptResult result)
    {
        var (text, brush) = result.Status switch
        {
            InterceptStatus.Opened => ("已打开", Green),
            InterceptStatus.OpenedWithFallback => ("回退打开", Green),
            InterceptStatus.SkippedPaused => ("已暂停", Gray),
            InterceptStatus.SkippedByRule => ("规则跳过", Gray),
            InterceptStatus.SkippedUnsupportedType => ("类型不支持", Gray),
            InterceptStatus.SkippedDuplicate => ("重复忽略", Gray),
            InterceptStatus.ValidationFailed => ("校验未通过", Amber),
            InterceptStatus.ViewerMissing => ("缺少查看器", Amber),
            InterceptStatus.LaunchFailed => ("启动失败", Red),
            _ => ("处理出错", Red)
        };

        var fileName = Path.GetFileName(result.Request.FilePath);
        var typeText = result.FileType is null
            ? "未知类型"
            : $"{result.FileType.DisplayName}";
        var message = string.IsNullOrWhiteSpace(result.Message) ? string.Empty : result.Message;

        return new EventRow(
            result.CompletedAt.ToLocalTime().ToString("HH:mm:ss"),
            text,
            brush,
            fileName,
            typeText,
            message);
    }
}
