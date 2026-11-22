using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using OutOpen.Core.Interop;
using OutOpen.Core.Models;

namespace OutOpen.Core.Launching;

/// <summary>文件启动端口，便于测试替换。</summary>
public interface IFileLauncher
{
    /// <summary>使用 Windows 默认关联程序打开。</summary>
    LaunchResult OpenWithDefault(string filePath);

    /// <summary>使用指定程序打开；程序不可用时返回失败。</summary>
    LaunchResult OpenWithProgram(string programPath, string filePath);
}

/// <summary>通过 Windows Shell 调用外部程序。</summary>
public sealed class ShellLauncher : IFileLauncher
{
    public LaunchResult OpenWithDefault(string filePath)
    {
        try
        {
            var info = new NativeMethods.ShellExecuteInfo
            {
                cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.ShellExecuteInfo>(),
                fMask = NativeMethods.SeeMaskNoCloseProcess | NativeMethods.SeeMaskFlagNoUi,
                lpVerb = "open",
                lpFile = filePath,
                nShow = NativeMethods.SwShowNormal
            };

            if (!NativeMethods.ShellExecuteExW(ref info))
            {
                var message = NativeMethods.DescribeLastError();
                return LaunchResult.Fail($"ShellExecuteEx 调用失败：{message}");
            }

            var processId = 0u;
            if (info.hProcess != IntPtr.Zero)
            {
                processId = NativeMethods.GetProcessId(info.hProcess);
                NativeMethods.CloseHandle(info.hProcess);
            }

            return LaunchResult.Ok(null, processId == 0 ? null : (int)processId);
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail("调用系统默认程序失败：" + ex.Message);
        }
    }

    public LaunchResult OpenWithProgram(string programPath, string filePath)
    {
        if (string.IsNullOrWhiteSpace(programPath))
        {
            return LaunchResult.Fail("未指定打开程序");
        }

        var expanded = Environment.ExpandEnvironmentVariables(programPath.Trim().Trim('"'));
        if (!File.Exists(expanded))
        {
            return LaunchResult.Fail($"指定的程序不存在：{expanded}", expanded);
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = expanded,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(expanded) ?? string.Empty
            };
            startInfo.ArgumentList.Add(filePath);

            using var process = Process.Start(startInfo);
            return LaunchResult.Ok(expanded, process?.Id);
        }
        catch (Win32Exception ex)
        {
            return LaunchResult.Fail($"启动 {Path.GetFileName(expanded)} 失败：{ex.Message}", expanded);
        }
        catch (Exception ex)
        {
            return LaunchResult.Fail($"启动外部程序失败：{ex.Message}", expanded);
        }
    }
}
