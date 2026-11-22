using System.IO;
using OutOpen.Core.Config;
using OutOpen.Core.Launching;

namespace OutOpen.Core.Viewers;

/// <summary>检测某扩展名的默认查看器状态，并在缺失时从本地推荐库给出官方下载入口。</summary>
public sealed class ViewerAdvisor
{
    private readonly IDefaultProgramResolver _resolver;
    private readonly ViewerCatalog _catalog;
    private readonly Func<DateTimeOffset> _clock;

    public ViewerAdvisor(
        IDefaultProgramResolver resolver,
        ViewerCatalog catalog,
        Func<DateTimeOffset>? clock = null)
    {
        _resolver = resolver;
        _catalog = catalog;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public ViewerCatalog Catalog => _catalog;

    public ViewerAssessment Assess(OutOpenConfig config, string extension, string? mimeType = null)
    {
        var normalized = Normalize(extension);
        var info = _resolver.Resolve(normalized);

        var status = info.Status switch
        {
            DefaultProgramStatus.Available => ViewerStatusKind.Available,
            DefaultProgramStatus.PackagedApplication => ViewerStatusKind.PackagedApplication,
            DefaultProgramStatus.Broken => ViewerStatusKind.BrokenAssociation,
            _ => ViewerStatusKind.NoAssociation
        };

        var recommendations = _catalog.ForExtension(normalized)
            .Select(e => e.ToRecommendation())
            .ToList();

        var state = config.RecommendationStates.TryGetValue(normalized, out var stored) ? stored : null;
        var now = _clock();
        var snoozeUntil = state?.SnoozeUntil;
        var suppressed = state?.Ignored == true || (snoozeUntil is { } until && until > now);

        var needsRecommendation = status is ViewerStatusKind.NoAssociation or ViewerStatusKind.BrokenAssociation;
        ViewerRecommendation? primary = null;
        IReadOnlyList<ViewerRecommendation> alternatives;

        if (needsRecommendation && !suppressed && recommendations.Count > 0)
        {
            primary = recommendations[0];
            alternatives = recommendations.Skip(1).ToList();
        }
        else
        {
            alternatives = recommendations;
        }

        return new ViewerAssessment(
            normalized,
            string.IsNullOrWhiteSpace(mimeType) ? string.Empty : mimeType,
            status,
            info.ExecutablePath,
            Describe(status, info),
            primary,
            alternatives,
            suppressed,
            snoozeUntil);
    }

    /// <summary>记录"稍后提醒"。</summary>
    public void Snooze(OutOpenConfig config, string extension, TimeSpan duration)
    {
        var key = Normalize(extension);
        config.RecommendationStates[key] = new RecommendationState
        {
            Ignored = false,
            SnoozeUntil = _clock().Add(duration)
        };
    }

    /// <summary>记录"不再提醒"。</summary>
    public void Ignore(OutOpenConfig config, string extension)
    {
        config.RecommendationStates[Normalize(extension)] = new RecommendationState
        {
            Ignored = true,
            SnoozeUntil = null
        };
    }

    /// <summary>重新启用某扩展名的推荐提醒。</summary>
    public void Reset(OutOpenConfig config, string extension)
    {
        config.RecommendationStates.Remove(Normalize(extension));
    }

    public static string Normalize(string extension) =>
        string.IsNullOrWhiteSpace(extension)
            ? string.Empty
            : (extension.StartsWith('.') ? extension : "." + extension).ToLowerInvariant();

    private static string Describe(ViewerStatusKind status, DefaultProgramInfo info) => status switch
    {
        ViewerStatusKind.Available =>
            $"已关联默认程序：{(string.IsNullOrWhiteSpace(info.FriendlyName) ? Path.GetFileName(info.ExecutablePath) : info.FriendlyName)}",
        ViewerStatusKind.PackagedApplication =>
            "已关联 Windows 打包应用（如系统照片），通常可直接打开",
        ViewerStatusKind.BrokenAssociation =>
            "默认程序已失效，可能是程序被卸载但注册表残留：" + (info.Error ?? "可执行文件不存在"),
        _ => "系统没有为该类型关联默认程序"
    };
}
