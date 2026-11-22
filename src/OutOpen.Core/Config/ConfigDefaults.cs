using OutOpen.Core.Models;

namespace OutOpen.Core.Config;

/// <summary>生成默认配置。</summary>
public static class ConfigDefaults
{
    public static OpenAction DefaultActionFor(FileCategory category) =>
        category is FileCategory.Image or FileCategory.Archive ? OpenAction.Skip : OpenAction.ExternalOpen;

    public static OutOpenConfig Create()
    {
        var config = new OutOpenConfig
        {
            Version = OutOpenConfig.CurrentVersion,
            Paused = false,
            Settings = new GeneralSettings()
        };

        foreach (var app in Catalog.SourceApps.All)
        {
            config.Sources[app.Id] = new SourceSettings { Enabled = app.EnabledByDefault };
        }

        foreach (var type in Catalog.SupportedTypes.All)
        {
            config.Extensions[type.Extension] = new ExtensionSettings
            {
                Enabled = true,
                Action = DefaultActionFor(type.Category)
            };
        }

        // 用户手动添加的扩展名（不在支持清单中）也允许配置，默认不接管。
        return config;
    }
}
