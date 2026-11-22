namespace OutOpen.Core.Catalog;

/// <summary>来源应用定义：进程名、默认监听目录与验证状态。</summary>
/// <param name="Id">稳定 id（写入配置）。</param>
/// <param name="DisplayName">界面显示名。</param>
/// <param name="Priority">产品优先级 P0/P1/P2。</param>
/// <param name="ProcessNames">用于判断应用是否在运行。</param>
/// <param name="WatchRoots">默认监听目录模板，支持 %ENV% 与 * 通配。</param>
/// <param name="ExcludedSegments">命中这些路径片段的文件会跳过（缩略图、缓存等）。</param>
/// <param name="Notes">兼容性说明。</param>
/// <param name="EnabledByDefault">默认是否开启接管。</param>
/// <param name="VerificationStatus">本机/文档验证状态。</param>
public sealed record SourceAppDefinition(
    string Id,
    string DisplayName,
    string Priority,
    IReadOnlyList<string> ProcessNames,
    IReadOnlyList<string> WatchRoots,
    IReadOnlyList<string> ExcludedSegments,
    string Notes,
    bool EnabledByDefault,
    string VerificationStatus);

/// <summary>目标应用清单（与产品计划书 2.2 对应）。</summary>
public static class SourceApps
{
    public static IReadOnlyList<SourceAppDefinition> All { get; } =
    [
        new(
            "wechat",
            "微信 PC 版",
            "P0",
            ["WeChat", "Weixin", "WeChatAppEx"],
            [
                @"%USERPROFILE%\Documents\WeChat Files\*\FileStorage\File",
                @"%USERPROFILE%\Documents\xwechat_files\*\msg\file"
            ],
            ["\\Temp\\", "\\Cache\\", "\\Thumb\\", "\\Image\\"],
            "监听微信接收文件目录；文件落盘并稳定后交给外部程序。不修改微信本体，不注入进程。",
            true,
            "本机已验证目录结构（WeChat Files\\<wxid>\\FileStorage\\File），点击级拦截未验证"),

        new(
            "qq",
            "QQ PC 版（NT）",
            "P0",
            ["QQ", "QQNT"],
            [
                @"%USERPROFILE%\Documents\Tencent Files\*\nt_qq\nt_data\File",
                @"%USERPROFILE%\Documents\Tencent Files\*\FileRecv"
            ],
            ["\\Thumb\\", "\\nt_temp\\", "\\Cache\\", "\\Emoji\\", "\\avatar\\", "\\dataline\\", ".thumb", ".tmp"],
            "监听 QQ NT 文件缓存目录；QQ 不同版本缓存布局不同，可在设置中补充监听目录。",
            true,
            "本机已验证 nt_qq\\nt_data\\File 目录存在（仅有缩略图缓存），接收文件落盘未验证"),

        new(
            "dingtalk",
            "钉钉",
            "P1",
            ["DingTalk"],
            [@"%USERPROFILE%\Documents\DingTalk\*"],
            ["\\Image\\", "\\Emotion\\", "\\Cache\\", "\\temp\\"],
            "钉钉文件目录因版本和账号差异较大，默认关闭；确认目录后可在配置中开启并补充路径。",
            false,
            "未验证"),

        new(
            "baidunetdisk",
            "百度网盘",
            "P1",
            ["BaiduNetdisk"],
            [@"%USERPROFILE%\Documents\BaiduNetdiskDownload"],
            ["\\.thumbnails\\"],
            "仅监听默认下载目录；网盘内部预览无法通过该方式接管。",
            false,
            "未验证"),

        new(
            "aliyundrive",
            "阿里云盘",
            "P1",
            ["aDrive"],
            [@"%USERPROFILE%\Documents\AliyunDrive"],
            ["\\cache\\"],
            "仅监听默认下载目录；路径可能随版本变化，默认关闭。",
            false,
            "未验证"),

        new(
            "onedrive",
            "OneDrive",
            "P2",
            ["OneDrive"],
            [@"%USERPROFILE%\OneDrive"],
            ["\\.git\\", "\\node_modules\\"],
            "OneDrive 本地同步目录包含全部云文件，开启后会对所有同步文件触发接管，请谨慎。",
            false,
            "未验证"),

        new(
            "telegram",
            "Telegram Desktop",
            "P2",
            [],
            [],
            [],
            "Telegram 默认把文件下载到用户下载目录，没有稳定的应用内缓存路径，暂不支持自动识别。",
            false,
            "不支持自动识别")
    ];

    public static SourceAppDefinition? Find(string id)
    {
        foreach (var app in All)
        {
            if (string.Equals(app.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return app;
            }
        }

        return null;
    }

    public static SourceAppDefinition Manual { get; } = new(
        "manual",
        "手动 / 拖拽",
        "—",
        [],
        [],
        [],
        "由用户拖拽文件到 OutOpen 窗口或命令行触发。",
        true,
        "可用");
}
