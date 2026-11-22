using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace OutOpen.Core.Diagnostics;

/// <summary>日志脱敏：日志里保留可诊断的信息，但隐藏用户名与账号类标识。</summary>
public static partial class PathMasker
{
    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>隐藏用户目录、微信 wxid、QQ 号等标识。</summary>
    public static string MaskPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var masked = path;

        if (!string.IsNullOrEmpty(UserProfile) && masked.StartsWith(UserProfile, StringComparison.OrdinalIgnoreCase))
        {
            masked = "%USERPROFILE%" + masked[UserProfile.Length..];
        }

        masked = WxidRegex().Replace(masked, "wxid_***");
        masked = DigitsRegex().Replace(masked, "***");

        return masked;
    }

    /// <summary>去掉路径中的文件名，只保留目录，避免日志记录具体文档名。</summary>
    public static string MaskDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            var directory = Path.GetDirectoryName(path);
            return MaskPath(string.IsNullOrEmpty(directory) ? path : directory);
        }
        catch (Exception)
        {
            return MaskPath(path);
        }
    }

    [GeneratedRegex(@"wxid_[0-9a-zA-Z_]+", RegexOptions.IgnoreCase)]
    private static partial Regex WxidRegex();

    [GeneratedRegex(@"\d{5,}")]
    private static partial Regex DigitsRegex();
}

/// <summary>日志端口。</summary>
public interface IAppLog
{
    void Info(string message, string category = "app");

    void Warn(string message, string category = "app");

    void Error(string message, string category = "app");
}

/// <summary>同时写入控制台的简单日志，供 CLI 与测试使用。</summary>
public sealed class ConsoleAppLog : IAppLog
{
    public bool Verbose { get; set; } = true;

    public void Info(string message, string category = "app")
    {
        if (Verbose)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [INFO ] [{category}] {message}");
        }
    }

    public void Warn(string message, string category = "app") =>
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [WARN ] [{category}] {message}");

    public void Error(string message, string category = "app") =>
        Console.Error.WriteLine($"[{DateTime.Now:HH:mm:ss}] [ERROR] [{category}] {message}");
}

/// <summary>按天滚动的本地日志文件，自动清理过期日志。</summary>
public sealed class FileAppLog : IAppLog, IDisposable
{
    private readonly string _directory;
    private readonly int _retentionDays;
    private readonly object _gate = new();

    public FileAppLog(string logDirectory, int retentionDays = 14)
    {
        _directory = logDirectory;
        _retentionDays = Math.Clamp(retentionDays, 1, 365);
        Directory.CreateDirectory(_directory);
        Cleanup();
    }

    public void Info(string message, string category = "app") => Write("INFO", category, message);

    public void Warn(string message, string category = "app") => Write("WARN", category, message);

    public void Error(string message, string category = "app") => Write("ERROR", category, message);

    public void Dispose()
    {
    }

    private void Write(string level, string category, string message)
    {
        var line = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level,-5}] [{category}] {message}{Environment.NewLine}";
        var file = Path.Combine(_directory, $"outopen-{DateTime.Now:yyyyMMdd}.log");

        lock (_gate)
        {
            try
            {
                File.AppendAllText(file, line, Encoding.UTF8);
            }
            catch (Exception)
            {
                // 日志失败不能影响主流程。
            }
        }
    }

    private void Cleanup()
    {
        try
        {
            var threshold = DateTime.Now.AddDays(-_retentionDays);
            foreach (var file in Directory.EnumerateFiles(_directory, "outopen-*.log"))
            {
                if (File.GetLastWriteTime(file) < threshold)
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception)
        {
        }
    }
}
