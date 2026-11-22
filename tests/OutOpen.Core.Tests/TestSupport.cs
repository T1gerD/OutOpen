using System.IO;
using OutOpen.Core.Launching;
using OutOpen.Core.Models;

namespace OutOpen.Core.Tests;

/// <summary>测试用临时目录，自动清理。</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "outopen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Write(string name, params byte[] bytes)
    {
        var full = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
        return full;
    }

    public string WriteText(string name, string text) => Write(name, System.Text.Encoding.UTF8.GetBytes(text));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (Exception)
        {
        }
    }
}

/// <summary>记录调用的假启动器。</summary>
internal sealed class FakeLauncher : IFileLauncher
{
    public bool DefaultSucceeds { get; set; } = true;

    public bool ProgramSucceeds { get; set; } = true;

    public List<string> Calls { get; } = [];

    public List<string> DefaultOpened { get; } = [];

    public List<string> ProgramOpened { get; } = [];

    public LaunchResult OpenWithDefault(string filePath)
    {
        Calls.Add("default:" + filePath);
        DefaultOpened.Add(filePath);
        return DefaultSucceeds ? LaunchResult.Ok(null, 4242) : LaunchResult.Fail("默认程序启动失败（测试）");
    }

    public LaunchResult OpenWithProgram(string programPath, string filePath)
    {
        Calls.Add("program:" + programPath + "|" + filePath);
        ProgramOpened.Add(filePath);
        return ProgramSucceeds
            ? LaunchResult.Ok(programPath, 5252)
            : LaunchResult.Fail("指定程序启动失败（测试）", programPath);
    }
}

/// <summary>可控的默认程序解析器。</summary>
internal sealed class FakeResolver : IDefaultProgramResolver
{
    public DefaultProgramInfo Info { get; set; } = new(
        DefaultProgramStatus.Available,
        @"C:\Windows\System32\notepad.exe",
        "记事本",
        null);

    public List<string> Queried { get; } = [];

    public DefaultProgramInfo Resolve(string extension)
    {
        Queried.Add(extension);
        return Info;
    }
}

internal static class SampleFiles
{
    public static readonly byte[] Pdf = System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF\n");

    public static readonly byte[] Zip = [0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00];

    public static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    public static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00];

    public static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00];

    public static readonly byte[] Rar = [(byte)'R', (byte)'a', (byte)'r', (byte)'!', 0x1A, 0x07, 0x00, 0x00];

    public static readonly byte[] SevenZip = [0x37, 0x7A, 0xBC, 0xAF, 0x27, 0x1C, 0x00, 0x00];

    public static readonly byte[] Gif = System.Text.Encoding.ASCII.GetBytes("GIF89a\x01\x00\x01\x00");
}
