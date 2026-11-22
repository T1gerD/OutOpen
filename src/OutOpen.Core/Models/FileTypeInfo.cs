namespace OutOpen.Core.Models;

/// <summary>文件大类，用于规则默认值与界面展示。</summary>
public enum FileCategory
{
    Document,
    Spreadsheet,
    Presentation,
    Text,
    Image,
    Archive,
    Other
}

/// <summary>文件类型识别结果（扩展名 + MIME + 文件签名）。</summary>
/// <param name="Extension">小写扩展名，包含点号。</param>
/// <param name="MimeType">MIME 类型，未知时为空字符串。</param>
/// <param name="Category">文件大类。</param>
/// <param name="DisplayName">面向用户的可读类型名。</param>
/// <param name="Signature">文件签名描述，例如 "PDF"、"ZIP/OOXML"，无法识别时为 null。</param>
/// <param name="SignatureMatchesExtension">签名与扩展名是否一致；无法判断时为 true。</param>
/// <param name="IsSupported">是否在 OutOpen 支持的类型清单内。</param>
public sealed record FileTypeInfo(
    string Extension,
    string MimeType,
    FileCategory Category,
    string DisplayName,
    string? Signature,
    bool SignatureMatchesExtension,
    bool IsSupported)
{
    public string MimeDisplay => string.IsNullOrWhiteSpace(MimeType) ? "未知类型" : MimeType;

    public string SignatureDisplay => string.IsNullOrWhiteSpace(Signature) ? "未识别" : Signature;
}
