using System.IO;
using OutOpen.Core.Catalog;
using OutOpen.Core.Models;

namespace OutOpen.Core.Detection;

/// <summary>根据扩展名、MIME 映射与文件头签名识别文件类型。</summary>
public sealed class FileTypeDetector
{
    public const int HeadSampleSize = 512;

    private static readonly Dictionary<string, string> ExtraMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".htm"] = "text/html",
        [".html"] = "text/html",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".mp4"] = "video/mp4",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".exe"] = "application/vnd.microsoft.portable-executable",
        [".dll"] = "application/vnd.microsoft.portable-executable"
    };

    /// <summary>读取文件头并识别类型；文件不可读时仅按扩展名推断。</summary>
    public FileTypeInfo Detect(string filePath)
    {
        var extension = NormalizeExtension(Path.GetExtension(filePath));
        Span<byte> head = stackalloc byte[HeadSampleSize];
        var count = TryReadHead(filePath, head);
        return DetectFromHead(filePath, extension, head[..count]);
    }

    /// <summary>使用已读取的文件头样本识别类型（便于单元测试）。</summary>
    public FileTypeInfo DetectFromHead(string filePath, ReadOnlySpan<byte> head)
    {
        var extension = NormalizeExtension(Path.GetExtension(filePath));
        return DetectFromHead(filePath, extension, head);
    }

    private FileTypeInfo DetectFromHead(string filePath, string extension, ReadOnlySpan<byte> head)
    {
        _ = filePath;
        var type = SupportedTypes.Find(extension);
        var signature = Classify(head);
        var expected = ExpectedSignatures(extension);

        // 空文件或无法识别的文件头不判定为签名不符，交给校验阶段处理。
        var matches = signature == SignatureKind.Unknown
            || expected.Count == 0
            || expected.Contains(signature);

        var mime = type?.MimeType
            ?? (ExtraMimeTypes.TryGetValue(extension, out var extra) ? extra : "application/octet-stream");

        return new FileTypeInfo(
            extension,
            mime,
            type?.Category ?? FileCategory.Other,
            type?.DisplayName ?? "其他文件",
            SignatureName(signature),
            matches,
            type is not null);
    }

    /// <summary>识别文件头签名。</summary>
    public static SignatureKind Classify(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 4 && head[0] == (byte)'%' && head[1] == (byte)'P' && head[2] == (byte)'D' && head[3] == (byte)'F')
        {
            return SignatureKind.Pdf;
        }

        if (head.Length >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04)
        {
            return SignatureKind.Zip;
        }

        if (head.Length >= 8 && head[0] == 0xD0 && head[1] == 0xCF && head[2] == 0x11 && head[3] == 0xE0
            && head[4] == 0xA1 && head[5] == 0xB1 && head[6] == 0x1A && head[7] == 0xE1)
        {
            return SignatureKind.Ole;
        }

        if (head.Length >= 7 && head[0] == (byte)'R' && head[1] == (byte)'a' && head[2] == (byte)'r'
            && head[3] == (byte)'!' && head[4] == 0x1A && head[5] == 0x07)
        {
            return SignatureKind.Rar;
        }

        if (head.Length >= 6 && head[0] == 0x37 && head[1] == 0x7A && head[2] == 0xBC && head[3] == 0xAF
            && head[4] == 0x27 && head[5] == 0x1C)
        {
            return SignatureKind.SevenZip;
        }

        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
        {
            return SignatureKind.Jpeg;
        }

        if (head.Length >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47
            && head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A)
        {
            return SignatureKind.Png;
        }

        if (head.Length >= 6 && head[0] == (byte)'G' && head[1] == (byte)'I' && head[2] == (byte)'F'
            && head[3] == (byte)'8' && (head[4] == (byte)'7' || head[4] == (byte)'9') && head[5] == (byte)'a')
        {
            return SignatureKind.Gif;
        }

        return SignatureKind.Unknown;
    }

    public static string? SignatureName(SignatureKind kind) => kind switch
    {
        SignatureKind.Pdf => "PDF",
        SignatureKind.Zip => "ZIP / OOXML",
        SignatureKind.Ole => "OLE 复合文档（Office 97-2003）",
        SignatureKind.Rar => "RAR",
        SignatureKind.SevenZip => "7-Zip",
        SignatureKind.Jpeg => "JPEG",
        SignatureKind.Png => "PNG",
        SignatureKind.Gif => "GIF",
        _ => null
    };

    /// <summary>扩展名允许的签名集合；空集合表示不做签名约束。</summary>
    public static IReadOnlySet<SignatureKind> ExpectedSignatures(string extension) =>
        NormalizeExtension(extension) switch
        {
            ".pdf" => Set(SignatureKind.Pdf),
            ".docx" or ".xlsx" or ".pptx" or ".zip" => Set(SignatureKind.Zip),
            ".doc" or ".xls" or ".ppt" => Set(SignatureKind.Ole),
            ".rar" => Set(SignatureKind.Rar),
            ".7z" => Set(SignatureKind.SevenZip),
            ".jpg" or ".jpeg" => Set(SignatureKind.Jpeg),
            ".png" => Set(SignatureKind.Png),
            ".gif" => Set(SignatureKind.Gif),
            _ => new HashSet<SignatureKind>()
        };

    internal static int TryReadHead(string filePath, Span<byte> buffer)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.Read(buffer);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    private static string NormalizeExtension(string extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : (extension.StartsWith('.') ? extension : "." + extension).ToLowerInvariant();

    private static HashSet<SignatureKind> Set(params SignatureKind[] kinds) => [.. kinds];
}

/// <summary>文件头签名分类。</summary>
public enum SignatureKind
{
    Unknown,
    Pdf,
    Zip,
    Ole,
    Rar,
    SevenZip,
    Jpeg,
    Png,
    Gif
}
