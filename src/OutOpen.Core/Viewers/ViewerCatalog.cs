using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OutOpen.Core.Viewers;

/// <summary>推荐库中的软件条目。</summary>
public sealed class ViewerCatalogEntry
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>适用扩展名（小写，含点号）。</summary>
    public List<string> Extensions { get; set; } = [];

    public string Purpose { get; set; } = string.Empty;

    /// <summary>官方产品页（优先展示）。</summary>
    public string OfficialUrl { get; set; } = string.Empty;

    /// <summary>官方下载页；没有单独下载页时为空。</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>来源说明，例如 "官方产品页"。</summary>
    public string? SourceNote { get; set; }

    /// <summary>链接最后核验日期（yyyy-MM-dd）。</summary>
    public string VerifiedAt { get; set; } = string.Empty;

    /// <summary>数值越小越优先。</summary>
    public int Priority { get; set; } = 50;

    public bool Enabled { get; set; } = true;

    public ViewerRecommendation ToRecommendation() => new(
        Id,
        Name,
        Purpose,
        OfficialUrl,
        DownloadUrl,
        VerifiedAt,
        SourceNote,
        Priority,
        Extensions);

    public bool Supports(string extension) =>
        Extensions.Any(e => string.Equals(e, extension, StringComparison.OrdinalIgnoreCase));
}

/// <summary>本地查看器推荐库（JSON）。</summary>
public sealed class ViewerCatalog
{
    public int Version { get; set; } = 1;

    public string VerifiedAt { get; set; } = "2026-11-22";

    public List<ViewerCatalogEntry> Entries { get; set; } = [];

    public IReadOnlyList<ViewerCatalogEntry> ForExtension(string extension)
    {
        var normalized = extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();

        return Entries
            .Where(e => e.Enabled && e.Supports(normalized))
            .OrderBy(e => e.Priority)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>内置推荐库。链接于 2026-11-22 逐条访问核验（HTTP 200）。</summary>
    public static ViewerCatalog CreateDefault()
    {
        const string verified = "2026-11-22";
        const string official = "官方产品页";

        return new ViewerCatalog
        {
            Version = 1,
            VerifiedAt = verified,
            Entries =
            [
                new ViewerCatalogEntry
                {
                    Id = "maxink",
                    Name = "MaxInk",
                    Extensions = [".md", ".markdown"],
                    Purpose = "轻量原生 Markdown 读写器，Windows 10/11 x64 安装包约 15 MB。",
                    OfficialUrl = "https://www.runxun.com/ai/tools/maxink",
                    DownloadUrl = "https://www.runxun.com/ai/tools/maxink/download",
                    SourceNote = official + "（润迅）；Windows 版已使用 Certum 数字签名",
                    VerifiedAt = verified,
                    Priority = 10
                },
                new ViewerCatalogEntry
                {
                    Id = "obsidian",
                    Name = "Obsidian",
                    Extensions = [".md", ".markdown"],
                    Purpose = "基于本地文件夹的 Markdown 笔记工具，支持双链与插件。",
                    OfficialUrl = "https://obsidian.md/download",
                    SourceNote = official,
                    VerifiedAt = verified,
                    Priority = 20
                },
                new ViewerCatalogEntry
                {
                    Id = "vscode",
                    Name = "Visual Studio Code",
                    Extensions = [".md", ".markdown", ".txt", ".json", ".csv"],
                    Purpose = "代码编辑器，内置 Markdown 预览，适合技术文档。",
                    OfficialUrl = "https://code.visualstudio.com/Download",
                    SourceNote = official,
                    VerifiedAt = verified,
                    Priority = 30
                },
                new ViewerCatalogEntry
                {
                    Id = "adobe-acrobat-reader",
                    Name = "Adobe Acrobat Reader",
                    Extensions = [".pdf"],
                    Purpose = "PDF 阅读、批注与表单填写。",
                    OfficialUrl = "https://get.adobe.com/reader/",
                    SourceNote = official,
                    VerifiedAt = verified,
                    Priority = 10
                },
                new ViewerCatalogEntry
                {
                    Id = "sumatrapdf",
                    Name = "SumatraPDF",
                    Extensions = [".pdf"],
                    Purpose = "体积小、启动快的开源 PDF / 电子书阅读器。",
                    OfficialUrl = "https://www.sumatrapdfreader.org/download-free-pdf-viewer",
                    SourceNote = official + "（开源）",
                    VerifiedAt = verified,
                    Priority = 20
                },
                new ViewerCatalogEntry
                {
                    Id = "wps",
                    Name = "WPS Office",
                    Extensions = [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv"],
                    Purpose = "国产办公套件，兼容 Microsoft Office 格式。",
                    OfficialUrl = "https://www.wps.cn/",
                    SourceNote = official,
                    VerifiedAt = verified,
                    Priority = 10
                },
                new ViewerCatalogEntry
                {
                    Id = "libreoffice",
                    Name = "LibreOffice",
                    Extensions = [".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".csv"],
                    Purpose = "开源办公套件，支持 OOXML 与旧版 Office 格式。",
                    OfficialUrl = "https://www.libreoffice.org/download/download-libreoffice/",
                    SourceNote = official + "（开源）",
                    VerifiedAt = verified,
                    Priority = 20
                },
                new ViewerCatalogEntry
                {
                    Id = "7zip",
                    Name = "7-Zip",
                    Extensions = [".zip", ".7z", ".rar"],
                    Purpose = "开源压缩包管理器，支持 ZIP / 7z / RAR 解压。",
                    OfficialUrl = "https://www.7-zip.org/download.html",
                    SourceNote = official + "（开源）",
                    VerifiedAt = verified,
                    Priority = 10
                },
                new ViewerCatalogEntry
                {
                    Id = "winrar",
                    Name = "WinRAR",
                    Extensions = [".rar", ".zip"],
                    Purpose = "支持 RAR 的压缩包管理器（共享软件）。",
                    OfficialUrl = "https://www.rarlab.com/download.htm",
                    SourceNote = official,
                    VerifiedAt = verified,
                    Priority = 20
                },
                new ViewerCatalogEntry
                {
                    Id = "windows-photos",
                    Name = "Windows 照片",
                    Extensions = [".jpg", ".jpeg", ".png", ".gif"],
                    Purpose = "Windows 自带图片查看器，Microsoft Store 官方分发。",
                    OfficialUrl = "https://apps.microsoft.com/detail/9wzdncrfjbh4",
                    SourceNote = "Microsoft Store 官方页面",
                    VerifiedAt = verified,
                    Priority = 10
                },
                new ViewerCatalogEntry
                {
                    Id = "notepad-plus-plus",
                    Name = "Notepad++",
                    Extensions = [".txt", ".md", ".csv"],
                    Purpose = "Windows 平台的轻量文本编辑器。",
                    OfficialUrl = "https://notepad-plus-plus.org/downloads/",
                    SourceNote = official + "（开源）",
                    VerifiedAt = verified,
                    Priority = 20
                }
            ]
        };
    }
}

/// <summary>推荐库 JSON 读写：默认生成到 %LOCALAPPDATA%\OutOpen\viewer-catalog.json，用户可直接编辑。</summary>
public sealed class ViewerCatalogStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly string _path;

    public ViewerCatalogStore(string baseDirectory)
    {
        _path = Path.Combine(baseDirectory, "viewer-catalog.json");
    }

    public string CatalogPath => _path;

    public ViewerCatalog LoadOrCreate()
    {
        if (!File.Exists(_path))
        {
            var created = ViewerCatalog.CreateDefault();
            Save(created);
            return created;
        }

        try
        {
            var json = File.ReadAllText(_path);
            var catalog = JsonSerializer.Deserialize<ViewerCatalog>(json, SerializerOptions);
            if (catalog is null || catalog.Entries.Count == 0)
            {
                return ViewerCatalog.CreateDefault();
            }

            return catalog;
        }
        catch (Exception)
        {
            return ViewerCatalog.CreateDefault();
        }
    }

    public void Save(ViewerCatalog catalog)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(catalog, SerializerOptions);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _path, overwrite: true);
    }
}
