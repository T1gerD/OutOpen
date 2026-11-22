using OutOpen.Core.Catalog;
using OutOpen.Core.Config;
using OutOpen.Core.Models;

namespace OutOpen.Core.Rules;

/// <summary>根据配置与高级规则决定如何处理一次打开请求。</summary>
public sealed class RulesEngine
{
    /// <summary>规则引擎的判定顺序：暂停 → 自动打开开关 → 来源应用 → 文件类型 → 高级规则 → 默认策略。</summary>
    public RuleDecision Decide(OutOpenConfig config, OpenRequest request, FileTypeInfo? fileType)
    {
        if (config.Paused)
        {
            return RuleDecision.Skip("OutOpen 已暂停接管", "paused");
        }

        var isAutomatic = request.Trigger is OpenTrigger.FolderArrival or OpenTrigger.StartupScan;
        if (isAutomatic && !config.Settings.AutoOpenOnArrival)
        {
            return RuleDecision.Skip("自动打开已关闭，只记录事件", "auto-open-off");
        }

        if (isAutomatic && !IsSourceEnabled(config, request.SourceAppId))
        {
            var name = string.IsNullOrWhiteSpace(request.SourceDisplayName) ? request.SourceAppId : request.SourceDisplayName;
            return RuleDecision.Skip($"未开启对「{name}」的接管", "source-disabled");
        }

        var extension = ConfigStore.NormalizeExtension(request.Extension);
        if (extension.Length == 0)
        {
            return RuleDecision.Skip("文件没有扩展名，无法判断如何打开", "no-extension");
        }

        var supported = SupportedTypes.Find(extension);
        if (supported is null)
        {
            return RuleDecision.Skip($"暂不支持 {extension} 类型", "unsupported-type");
        }

        var extensionSettings = ConfigStore.GetExtensionSettings(config, extension);
        if (!extensionSettings.Enabled)
        {
            return RuleDecision.Skip($"{supported.DisplayName} 已关闭自动打开", "extension-disabled");
        }

        var rule = FindMatchingRule(config, request, extension);
        if (rule is not null)
        {
            return new RuleDecision(rule.Action, rule.TargetProgram, $"命中高级规则 {rule.Id}", rule.Id);
        }

        return new RuleDecision(
            extensionSettings.Action,
            extensionSettings.TargetProgram,
            $"{supported.DisplayName} 的默认策略：{Describe(extensionSettings.Action)}",
            "extension-policy");
    }

    public static bool IsSourceEnabled(OutOpenConfig config, string sourceAppId)
    {
        if (string.IsNullOrWhiteSpace(sourceAppId))
        {
            return false;
        }

        if (string.Equals(sourceAppId, SourceApps.Manual.Id, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return config.Sources.TryGetValue(sourceAppId, out var settings) && settings.Enabled;
    }

    private static RuleSettings? FindMatchingRule(OutOpenConfig config, OpenRequest request, string extension)
    {
        RuleSettings? best = null;

        foreach (var rule in config.Rules)
        {
            if (!rule.Extensions.Any(e => string.Equals(ConfigStore.NormalizeExtension(e), extension, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(rule.SourceAppId)
                && !string.Equals(rule.SourceAppId, request.SourceAppId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (best is null || rule.Priority > best.Priority)
            {
                best = rule;
            }
        }

        return best;
    }

    public static string Describe(OpenAction action) => action switch
    {
        OpenAction.ExternalOpen => "外部打开",
        OpenAction.SystemDefault => "交给系统默认程序",
        OpenAction.Skip => "不接管",
        _ => action.ToString()
    };
}
