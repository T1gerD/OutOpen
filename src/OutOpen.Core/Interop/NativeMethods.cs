using System.Runtime.InteropServices;
using System.Text;

namespace OutOpen.Core.Interop;

internal static class NativeMethods
{
    public const uint SeeMaskNoCloseProcess = 0x00000040;
    public const uint SeeMaskFlagNoUi = 0x00000400;
    public const int SwShowNormal = 1;

    /// <summary>Windows Shell 的 SHELLEXECUTEINFOW。</summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct ShellExecuteInfo
    {
        public int cbSize;
        public uint fMask;
        public IntPtr hwnd;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpVerb;
        [MarshalAs(UnmanagedType.LPWStr)] public string lpFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpParameters;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpDirectory;
        public int nShow;
        public IntPtr hInstApp;
        public IntPtr lpIDList;
        [MarshalAs(UnmanagedType.LPWStr)] public string? lpClass;
        public IntPtr hkeyClass;
        public uint dwHotKey;
        public IntPtr hIconOrMonitor;
        public IntPtr hProcess;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ShellExecuteExW(ref ShellExecuteInfo executeInfo);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint GetProcessId(IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);

    // AssocQueryString 常量。
    public const uint AssocFNone = 0;
    public const uint AssocStrExecutable = 2;
    public const uint AssocStrFriendlyAppName = 4;

    [DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
    internal static extern int AssocQueryStringW(
        uint flags,
        uint str,
        string pszAssoc,
        string? pszExtra,
        StringBuilder? pszOut,
        ref uint pcchOut);

    /// <summary>Win32 错误码转可读消息。</summary>
    public static string DescribeLastError()
    {
        var code = Marshal.GetLastWin32Error();
        return code == 0 ? "未知错误" : $"{new System.ComponentModel.Win32Exception(code).Message}（0x{code:X8}）";
    }
}
