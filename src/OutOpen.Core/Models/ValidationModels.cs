namespace OutOpen.Core.Models;

/// <summary>文件可用性检查中发现的问题。</summary>
public enum FileProblem
{
    NotFound,
    NotAFile,
    EmptyFile,
    TooLarge,
    TemporaryDownload,
    PathTooLong,
    AccessDenied,
    InUse,
    Unstable,
    SignatureMismatch
}

public sealed record FileProblemInfo(FileProblem Problem, string Message)
{
    public override string ToString() => Message;
}

/// <summary>文件校验报告：是否可用、大小、最后写入时间与问题列表。</summary>
public sealed record FileValidationReport
{
    public required string FilePath { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTimeOffset LastWriteTime { get; init; }

    public bool IsStable { get; init; } = true;

    /// <summary>识别出的文件类型；校验未通过时可能为空。</summary>
    public FileTypeInfo? FileType { get; init; }

    public IReadOnlyList<FileProblemInfo> Problems { get; init; } = [];

    public bool IsValid => Problems.Count == 0;

    /// <summary>首个问题的可读说明，用于日志与界面。</summary>
    public string Summary => Problems.Count == 0 ? "文件可用" : Problems[0].Message;

    public static FileValidationReport Invalid(string filePath, params FileProblemInfo[] problems) => new()
    {
        FilePath = filePath,
        SizeBytes = 0,
        LastWriteTime = DateTimeOffset.MinValue,
        Problems = problems
    };
}
