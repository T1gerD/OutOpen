using OutOpen.Core.Models;

namespace OutOpen.Core.Catalog;

/// <summary>OutOpen 支持的文件类型定义。</summary>
public sealed record SupportedFileType(
    string Extension,
    string MimeType,
    FileCategory Category,
    string DisplayName,
    OpenAction DefaultAction);

/// <summary>第一版支持的文件类型清单（与产品计划书 2.3 一致，另含少量常见补充类型）。</summary>
public static class SupportedTypes
{
    public static IReadOnlyList<SupportedFileType> All { get; } =
    [
        new(".pdf", "application/pdf", FileCategory.Document, "PDF 文档", OpenAction.ExternalOpen),
        new(".doc", "application/msword", FileCategory.Document, "Word 文档（97-2003）", OpenAction.ExternalOpen),
        new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", FileCategory.Document, "Word 文档", OpenAction.ExternalOpen),
        new(".xls", "application/vnd.ms-excel", FileCategory.Spreadsheet, "Excel 表格（97-2003）", OpenAction.ExternalOpen),
        new(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileCategory.Spreadsheet, "Excel 表格", OpenAction.ExternalOpen),
        new(".ppt", "application/vnd.ms-powerpoint", FileCategory.Presentation, "PowerPoint 演示（97-2003）", OpenAction.ExternalOpen),
        new(".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation", FileCategory.Presentation, "PowerPoint 演示", OpenAction.ExternalOpen),
        new(".txt", "text/plain", FileCategory.Text, "纯文本", OpenAction.ExternalOpen),
        new(".md", "text/markdown", FileCategory.Text, "Markdown 文档", OpenAction.ExternalOpen),
        new(".csv", "text/csv", FileCategory.Spreadsheet, "CSV 表格", OpenAction.ExternalOpen),
        new(".jpg", "image/jpeg", FileCategory.Image, "JPEG 图片", OpenAction.Skip),
        new(".jpeg", "image/jpeg", FileCategory.Image, "JPEG 图片", OpenAction.Skip),
        new(".png", "image/png", FileCategory.Image, "PNG 图片", OpenAction.Skip),
        new(".gif", "image/gif", FileCategory.Image, "GIF 图片", OpenAction.Skip),
        new(".zip", "application/zip", FileCategory.Archive, "ZIP 压缩包", OpenAction.Skip),
        new(".7z", "application/x-7z-compressed", FileCategory.Archive, "7-Zip 压缩包", OpenAction.Skip),
        new(".rar", "application/vnd.rar", FileCategory.Archive, "RAR 压缩包", OpenAction.Skip)
    ];

    public static SupportedFileType? Find(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        var normalized = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
        foreach (var type in All)
        {
            if (string.Equals(type.Extension, normalized, StringComparison.OrdinalIgnoreCase))
            {
                return type;
            }
        }

        return null;
    }

    public static bool IsSupported(string extension) => Find(extension) is not null;
}
