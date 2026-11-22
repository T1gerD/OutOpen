using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace OutOpen.Core.Startup;

/// <summary>开机自启：写入 HKCU\Software\Microsoft\Windows\CurrentVersion\Run，仅影响当前用户。</summary>
public sealed class AutoStartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "OutOpen";

    /// <summary>当前是否已注册自启。</summary>
    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }

    public string? RegisteredCommand
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) as string;
        }
    }

    /// <summary>启用或关闭自启；返回是否成功。</summary>
    public bool SetEnabled(bool enabled, string? executablePath = null)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key is null)
            {
                return false;
            }

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                return true;
            }

            var exe = executablePath ?? ResolveExecutable();
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                return false;
            }

            key.SetValue(ValueName, $"\"{exe}\" --tray", RegistryValueKind.String);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>解析真实的可执行文件：dotnet run/测试宿主下回退到应用自身 exe。</summary>
    private static string? ResolveExecutable()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) &&
            !Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase) &&
            !Path.GetFileName(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return processPath;
        }

        var entry = Assembly.GetEntryAssembly()?.Location;
        if (!string.IsNullOrWhiteSpace(entry))
        {
            var candidate = Path.ChangeExtension(entry, ".exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return processPath;
    }
}
