using System.IO;
using System.Text;
using OutOpen.Core.Interop;

namespace OutOpen.Core.Launching;

/// <summary>Windows 默认程序解析状态。</summary>
public enum DefaultProgramStatus
{
    /// <summary>存在可用的默认程序可执行文件。</summary>
    Available,

    /// <summary>系统没有为该类型关联程序。</summary>
    NoAssociation,

    /// <summary>有关联，但可执行文件已不存在（卸载残留）。</summary>
    Broken,

    /// <summary>关联的是打包应用 / 系统宿主，无法用普通路径判断。</summary>
    PackagedApplication,

    /// <summary>查询过程出错。</summary>
    Error
}

public sealed record DefaultProgramInfo(
    DefaultProgramStatus Status,
    string? ExecutablePath,
    string? FriendlyName,
    string? Error)
{
    public static DefaultProgramInfo NoAssociation { get; } = new(DefaultProgramStatus.NoAssociation, null, null, null);
}

/// <summary>默认程序查询端口。</summary>
public interface IDefaultProgramResolver
{
    DefaultProgramInfo Resolve(string extension);
}

/// <summary>使用 AssocQueryStringW 查询 Windows 文件关联。</summary>
public sealed class WindowsDefaultProgramResolver : IDefaultProgramResolver
{
    private const int ENoAssociation = unchecked((int)0x80070483);
    private const int NoError = 0;

    public DefaultProgramInfo Resolve(string extension)
    {
        var assoc = Normalize(extension);
        if (assoc.Length == 0)
        {
            return DefaultProgramInfo.NoAssociation;
        }

        var executable = Query(assoc, NativeMethods.AssocStrExecutable, out var error);
        if (error == ENoAssociation)
        {
            return DefaultProgramInfo.NoAssociation;
        }

        if (executable is null)
        {
            return new DefaultProgramInfo(DefaultProgramStatus.NoAssociation, null, null, null);
        }

        var friendly = Query(assoc, NativeMethods.AssocStrFriendlyAppName, out _);

        var fileName = Path.GetFileName(executable);
        if (string.Equals(fileName, "OpenWith.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "OpenWithHost.exe", StringComparison.OrdinalIgnoreCase))
        {
            // 仅注册了"打开方式"对话框，没有真正的默认程序。
            return new DefaultProgramInfo(DefaultProgramStatus.NoAssociation, null, friendly, null);
        }

        var isPackaged = executable.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase)
            || string.Equals(fileName, "ApplicationFrameHost.exe", StringComparison.OrdinalIgnoreCase)
            || executable.Contains("Microsoft.Windows.Photos", StringComparison.OrdinalIgnoreCase);

        if (isPackaged)
        {
            return new DefaultProgramInfo(DefaultProgramStatus.PackagedApplication, executable, friendly, null);
        }

        if (!File.Exists(executable))
        {
            return new DefaultProgramInfo(
                DefaultProgramStatus.Broken,
                executable,
                friendly,
                $"注册的默认程序已不存在：{executable}");
        }

        return new DefaultProgramInfo(DefaultProgramStatus.Available, executable, friendly, null);
    }

    private static string? Query(string assoc, uint what, out int error)
    {
        var buffer = new StringBuilder(1024);
        uint length = (uint)buffer.Capacity;
        var hr = NativeMethods.AssocQueryStringW(NativeMethods.AssocFNone, what, assoc, null, buffer, ref length);
        error = hr;
        if (hr != NoError)
        {
            return null;
        }

        var value = buffer.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string Normalize(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed.ToLowerInvariant() : "." + trimmed.ToLowerInvariant();
    }
}
