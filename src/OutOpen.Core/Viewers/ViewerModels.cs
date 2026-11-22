namespace OutOpen.Core.Viewers;

/// <summary>扩展名的默认程序状态。</summary>
public enum ViewerStatusKind
{
    /// <summary>存在可用的默认程序。</summary>
    Available,

    /// <summary>系统没有为该类型关联任何程序。</summary>
    NoAssociation,

    /// <summary>注册表里有打开方式，但程序路径已失效（卸载残留等）。</summary>
    BrokenAssociation,

    /// <summary>关联到系统打包应用（UWP），无法直接校验可执行文件但通常可用。</summary>
    PackagedApplication
}

/// <summary>查看器推荐条目（由本地推荐库 JSON 提供）。</summary>
public sealed record ViewerRecommendation(
    string Id,
    string Name,
    string Purpose,
    string OfficialUrl,
    string? DownloadUrl,
    string VerifiedAt,
    string? SourceNote,
    int Priority,
    IReadOnlyList<string> Extensions);

/// <summary>某个扩展名的查看器评估结果。</summary>
public sealed record ViewerAssessment(
    string Extension,
    string MimeType,
    ViewerStatusKind Status,
    string? DefaultProgramPath,
    string StatusText,
    ViewerRecommendation? Primary,
    IReadOnlyList<ViewerRecommendation> Alternatives,
    bool Suppressed,
    DateTimeOffset? SnoozeUntil)
{
    public bool HasViewer =>
        Status is ViewerStatusKind.Available or ViewerStatusKind.PackagedApplication;

    /// <summary>全部候选（首选 + 替代）。</summary>
    public IReadOnlyList<ViewerRecommendation> AllRecommendations
    {
        get
        {
            if (Primary is null)
            {
                return Alternatives;
            }

            var list = new List<ViewerRecommendation>(Alternatives.Count + 1) { Primary };
            list.AddRange(Alternatives);
            return list;
        }
    }
}
