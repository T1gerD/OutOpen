using System.Text;
using OutOpen.Core.Adapters;
using OutOpen.Core.Catalog;
using OutOpen.Core.Config;
using OutOpen.Core.Detection;
using OutOpen.Core.Hosting;
using OutOpen.Core.Launching;
using OutOpen.Core.Models;
using OutOpen.Core.Rules;
using OutOpen.Core.Startup;
using OutOpen.Core.Viewers;

namespace OutOpen.Cli;

/// <summary>
/// OutOpen 命令行诊断工具：不启动常驻监听，只读地展示配置、类型识别、校验、规则与查看器判定。
/// 只有显式的 <c>open</c> 子命令会真正调用外部程序。
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp();
            return 0;
        }

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "status" => Status(),
                "sources" => Sources(),
                "types" => Types(),
                "check" => Check(args.Skip(1).ToArray()),
                "viewers" => ViewersCommand(args.Skip(1).ToArray()),
                "autostart" => AutoStartCommand(args.Skip(1).ToArray()),
                "open" => Open(args.Skip(1).ToArray()),
                _ => Unknown(args[0])
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("出错：" + ex.Message);
            return 1;
        }
    }

    private static void PrintHelp()
    {
        Console.WriteLine("OutOpen CLI — Windows 文件打开行为接管工具（诊断）");
        Console.WriteLine();
        Console.WriteLine("用法：outopen <命令> [参数]");
        Console.WriteLine();
        Console.WriteLine("  status               显示数据目录、暂停状态、来源应用与监听目录");
        Console.WriteLine("  sources              列出支持接管的来源应用及本机实际目录");
        Console.WriteLine("  types                列出支持的文件类型与默认打开策略");
        Console.WriteLine("  check <文件>         识别类型、校验完整性、给出规则结论（不打开文件）");
        Console.WriteLine("  viewers <扩展名>     查看指定扩展名的关联状态与官方推荐");
        Console.WriteLine("  autostart [on|off]   查看或修改开机自启（写入当前用户启动项）");
        Console.WriteLine("  open <文件>          手动按当前规则打开一个文件（会启动外部程序）");
        Console.WriteLine();
        Console.WriteLine("示例：");
        Console.WriteLine("  outopen check \"C:\\Users\\me\\Downloads\\报告.pdf\"");
        Console.WriteLine("  outopen viewers .md");
    }

    private static int Unknown(string command)
    {
        Console.Error.WriteLine($"未知命令：{command}。运行 outopen --help 查看用法。");
        return 2;
    }

    private static int Status()
    {
        using var host = new OutOpenHost();
        var config = host.Config;
        var catalogPath = new ViewerCatalogStore(host.DataDirectory).CatalogPath;

        Console.WriteLine("OutOpen 诊断");
        Console.WriteLine("  版本            " + typeof(OutOpenHost).Assembly.GetName().Version);
        Console.WriteLine("  数据目录        " + host.DataDirectory);
        Console.WriteLine("  配置            " + host.ConfigStore.ConfigPath);
        Console.WriteLine("  查看器推荐库    " + catalogPath);
        Console.WriteLine("  日志目录        " + host.LogDirectory);
        Console.WriteLine("  接管状态        " + (config.Paused ? "已暂停" : "运行中（未启动托盘进程时仅读取配置）"));
        Console.WriteLine("  开机自启        " + (new AutoStartManager().IsEnabled ? "已注册" : "未注册"));
        Console.WriteLine();

        Console.WriteLine("来源应用：");
        foreach (var source in host.GetSources())
        {
            var state = source.Enabled ? "启用" : "关闭";
            Console.WriteLine(
                $"  [{source.Priority}] {source.DisplayName,-10} {state,-4} " +
                $"目录 {source.Folders.Count} 个");
            foreach (var folder in source.Folders)
            {
                Console.WriteLine("        " + folder);
            }
        }

        return 0;
    }

    private static int Sources()
    {
        Console.WriteLine("来源应用与监听目录（基于当前用户配置）：");
        foreach (var app in SourceApps.All)
        {
            Console.WriteLine();
            Console.WriteLine($"{app.DisplayName}（{app.Id}，优先级 {app.Priority}）");
            Console.WriteLine("  进程名：" + string.Join(", ", app.ProcessNames));
            Console.WriteLine("  说明：" + app.Notes);
            var folders = FolderResolver.ResolveFolders(app, null);
            if (folders.Count == 0)
            {
                Console.WriteLine("  本机目录：未找到（尚未安装或路径不同）");
            }
            else
            {
                foreach (var folder in folders)
                {
                    Console.WriteLine("  本机目录：" + folder);
                }
            }

            Console.WriteLine("  验证状态：" + app.VerificationStatus);
        }

        return 0;
    }

    private static int Types()
    {
        Console.WriteLine("支持的文件类型与默认策略：");
        foreach (var type in SupportedTypes.All)
        {
            var action = ConfigDefaults.DefaultActionFor(type.Category);
            Console.WriteLine(
                $"  {type.Extension,-10} {type.DisplayName,-8} {type.Category,-8} " +
                $"{RulesEngine.Describe(action),-8} {type.MimeType}");
        }

        return 0;
    }

    private static int Check(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.Error.WriteLine("用法：outopen check <文件>");
            return 2;
        }

        var path = rest[0];
        using var host = new OutOpenHost();
        var detector = host.Detector;
        var validator = host.Validator;

        Console.WriteLine("文件：" + path);
        Console.WriteLine("存在：" + File.Exists(path));

        var type = detector.Detect(path);
        Console.WriteLine("扩展名：" + type.Extension);
        Console.WriteLine("识别类型：" + type.DisplayName + "（" + type.Category + "）");
        Console.WriteLine("MIME：" + type.MimeType);
        Console.WriteLine("文件签名：" + (type.Signature ?? "无法识别"));
        Console.WriteLine("签名匹配：" + (type.SignatureMatchesExtension ? "是" : "否（扩展名与实际内容不一致）"));
        Console.WriteLine("支持类型：" + (type.IsSupported ? "是" : "否"));

        var options = new FileValidationOptions(
            host.Config.Settings.StabilitySeconds,
            host.Config.Settings.MaxFileSizeMb,
            host.Config.Settings.RequireSignatureMatch);
        var report = validator.Validate(path, options);
        Console.WriteLine("完整性校验：" + (report.IsValid ? "通过" : "未通过 — " + report.Summary));
        foreach (var problem in report.Problems)
        {
            Console.WriteLine("  - " + problem.Problem + "：" + problem.Message);
        }

        var request = new OpenRequest
        {
            FilePath = path,
            SourceAppId = SourceApps.Manual.Id,
            SourceDisplayName = SourceApps.Manual.DisplayName,
            Trigger = OpenTrigger.Manual
        };
        var decision = host.Rules.Decide(host.Config, request, type);
        Console.WriteLine("规则结论：" + decision.Action + " — " + decision.Reason);

        var assessment = host.ViewerAdvisor.Assess(host.Config, type.Extension, type.MimeType);
        Console.WriteLine("查看器：" + assessment.Status + " — " + assessment.StatusText);
        if (assessment.AllRecommendations.Count > 0)
        {
            Console.WriteLine("  官方推荐（需要时可自行安装）：");
            foreach (var recommendation in assessment.AllRecommendations)
            {
                Console.WriteLine($"    {recommendation.Name} · {recommendation.OfficialUrl} · 核验 {recommendation.VerifiedAt}");
            }
        }

        return report.IsValid ? 0 : 1;
    }

    private static int AutoStartCommand(string[] rest)
    {
        var action = rest.Length == 0 ? "status" : rest[0].ToLowerInvariant();
        var manager = new AutoStartManager();
        switch (action)
        {
            case "status":
                Console.WriteLine("开机自启：" + (manager.IsEnabled ? "已注册" : "未注册"));
                Console.WriteLine("注册命令：" + (manager.RegisteredCommand ?? "（无）"));
                return 0;
            case "on":
            case "off":
                var enable = action == "on";
                manager.SetEnabled(enable, ResolveDesktopHost());
                Console.WriteLine(enable ? "已注册开机自启（--tray 启动，最小化到托盘）。" : "已取消开机自启。");
                Console.WriteLine("注册命令：" + (manager.RegisteredCommand ?? "（无）"));
                return 0;
            default:
                Console.Error.WriteLine("用法：outopen autostart [status|on|off]");
                return 2;
        }
    }

    /// <summary>
    /// 开机自启必须指向桌面宿主（OutOpen.exe），不能是把 CLI 自己写进启动项——
    /// 否则登录后会拉起一个控制台进程。安装布局为 {app}\OutOpen.exe + {app}\cli\outopen.exe，
    /// publish 布局为 publish\app\OutOpen.exe + publish\cli\outopen.exe。
    /// </summary>
    private static string? ResolveDesktopHost()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(baseDir, "..", "OutOpen.exe")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "app", "OutOpen.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static int ViewersCommand(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.Error.WriteLine("用法：outopen viewers <扩展名>，例如 outopen viewers .md");
            return 2;
        }

        var extension = ConfigStore.NormalizeExtension(rest[0]);
        using var host = new OutOpenHost();
        var assessment = host.ViewerAdvisor.Assess(host.Config, extension);

        Console.WriteLine("扩展名：" + extension);
        Console.WriteLine("状态：" + assessment.Status + " — " + assessment.StatusText);
        Console.WriteLine("默认程序：" + (assessment.DefaultProgramPath ?? "（无）"));
        Console.WriteLine("建议打开：" + (assessment.HasViewer ? "默认关联" : "查看器推荐"));
        Console.WriteLine();

        if (assessment.AllRecommendations.Count == 0)
        {
            Console.WriteLine("推荐库中没有该扩展名的条目。");
            return 0;
        }

        foreach (var recommendation in assessment.AllRecommendations)
        {
            var mark = ReferenceEquals(recommendation, assessment.Primary) ? "*" : " ";
            Console.WriteLine($"{mark} {recommendation.Name}");
            Console.WriteLine($"    用途：{recommendation.Purpose}");
            Console.WriteLine($"    介绍：{recommendation.OfficialUrl}");
            Console.WriteLine($"    下载：{recommendation.DownloadUrl}");
            Console.WriteLine($"    来源：{recommendation.SourceNote}（核验 {recommendation.VerifiedAt}）");
        }

        Console.WriteLine();
        Console.WriteLine("OutOpen 不会自动下载或安装；请自行从上面的官方入口获取。");
        return 0;
    }

    private static int Open(string[] rest)
    {
        if (rest.Length == 0)
        {
            Console.Error.WriteLine("用法：outopen open <文件>");
            return 2;
        }

        using var host = new OutOpenHost();
        var result = host.HandleManual(rest[0]);
        Console.WriteLine("结果：" + result.Status + " — " + result.Message);

        if (result.Launch is { Success: true })
        {
            Console.WriteLine("已启动：" + result.Launch.ProgramPath);
        }

        if (result.Validation is { IsValid: false })
        {
            Console.WriteLine("校验：" + result.Validation.Summary);
        }

        return result.IsOpened ? 0 : 1;
    }
}
