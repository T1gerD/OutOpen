namespace OutOpen.Core.Models;

/// <summary>调用外部程序的结果。失败时保留可读错误信息，供界面与日志使用。</summary>
public sealed record LaunchResult
{
    public required bool Success { get; init; }

    /// <summary>实际使用的程序路径；使用系统默认程序时可能为 null。</summary>
    public string? ProgramPath { get; init; }

    public int? ProcessId { get; init; }

    public string? ErrorMessage { get; init; }

    public static LaunchResult Ok(string? programPath, int? processId = null) => new()
    {
        Success = true,
        ProgramPath = programPath,
        ProcessId = processId
    };

    public static LaunchResult Fail(string message, string? programPath = null) => new()
    {
        Success = false,
        ProgramPath = programPath,
        ErrorMessage = message
    };
}
