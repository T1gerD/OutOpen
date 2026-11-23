using System.Diagnostics;
using System.IO;
using OutOpen.Core.Models;

namespace OutOpen.Core.Detection;

/// <summary>文件校验选项。</summary>
/// <param name="StabilitySeconds">需要保持不变的秒数。</param>
/// <param name="MaxFileSizeMb">允许接管的文件大小上限（MB）。</param>
/// <param name="RequireSignatureMatch">是否要求文件头签名与扩展名一致。</param>
public sealed record FileValidationOptions(
    double StabilitySeconds = 2.0,
    int MaxFileSizeMb = 512,
    bool RequireSignatureMatch = false);

/// <summary>文件快照：大小与最后写入时间。</summary>
public readonly record struct FileSnapshot(long SizeBytes, DateTimeOffset LastWriteTime);

/// <summary>打开前的文件可用性校验：存在、非空、未超限、非临时文件、未被独占、下载已完成。</summary>
public sealed class FileValidationService
{
    private static readonly string[] TemporarySuffixes =
    [
        ".part", ".partial", ".tmp", ".temp", ".download", ".crdownload", ".opdownload",
        ".filepart", ".!ut", ".td", ".aria2", ".bc!", ".xltd", ".dl", ".wechat_temp",
        // 网盘客户端下载中的中间文件
        ".downloading", ".baiduyun.uploading", ".baiduyun.uploading.cfg", ".pd", ".blob", ".qdl", ".!qb"
    ];

    private readonly FileTypeDetector _detector;

    public FileValidationService(FileTypeDetector detector)
    {
        _detector = detector;
    }

    /// <summary>同步校验一次文件。</summary>
    public FileValidationReport Validate(string filePath, FileValidationOptions options)
    {
        var problems = new List<FileProblemInfo>();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return FileValidationReport.Invalid(filePath ?? string.Empty, new FileProblemInfo(FileProblem.NotFound, "文件路径为空"));
        }

        if (!File.Exists(filePath))
        {
            var message = Directory.Exists(filePath) ? "路径是目录，不是文件" : "文件不存在或已被移动";
            return FileValidationReport.Invalid(filePath,
                new FileProblemInfo(Directory.Exists(filePath) ? FileProblem.NotAFile : FileProblem.NotFound, message));
        }

        if (filePath.Length > 30000)
        {
            problems.Add(new FileProblemInfo(FileProblem.PathTooLong, "文件路径过长"));
        }

        var name = Path.GetFileName(filePath);
        if (IsTemporaryName(name))
        {
            problems.Add(new FileProblemInfo(FileProblem.TemporaryDownload, "文件仍是临时下载文件，尚未完成"));
        }

        var info = new FileInfo(filePath);
        long size = 0;
        DateTimeOffset lastWrite = DateTimeOffset.MinValue;
        try
        {
            size = info.Length;
            lastWrite = info.LastWriteTimeUtc;
            if (size == 0)
            {
                problems.Add(new FileProblemInfo(FileProblem.EmptyFile, "文件为空（0 字节），可能尚未写入完成"));
            }
            else if (size > (long)options.MaxFileSizeMb * 1024 * 1024)
            {
                problems.Add(new FileProblemInfo(FileProblem.TooLarge, $"文件超过 {options.MaxFileSizeMb} MB 上限，未接管"));
            }
        }
        catch (UnauthorizedAccessException)
        {
            problems.Add(new FileProblemInfo(FileProblem.AccessDenied, "没有读取该文件的权限"));
        }
        catch (IOException ex)
        {
            problems.Add(new FileProblemInfo(FileProblem.InUse, "读取文件信息失败：" + ex.Message));
        }

        if (problems.Count == 0)
        {
            var access = ProbeAccess(filePath);
            if (access is not null)
            {
                problems.Add(access);
            }
        }

        FileTypeInfo? type = null;
        if (problems.Count == 0)
        {
            type = _detector.Detect(filePath);
            if (options.RequireSignatureMatch && !type.SignatureMatchesExtension)
            {
                problems.Add(new FileProblemInfo(
                    FileProblem.SignatureMismatch,
                    $"文件内容与扩展名不一致（扩展名 {type.Extension}，实际签名 {type.SignatureDisplay}）"));
            }
        }

        _ = type;
        return new FileValidationReport
        {
            FilePath = filePath,
            SizeBytes = size,
            LastWriteTime = lastWrite,
            IsStable = true,
            FileType = type,
            Problems = problems
        };
    }

    /// <summary>轮询等待文件大小与写入时间稳定，返回最后一次快照；超时返回 null。</summary>
    public async Task<FileSnapshot?> WaitUntilStableAsync(string filePath, double stabilitySeconds, CancellationToken cancellationToken = default)
    {
        if (stabilitySeconds <= 0)
        {
            return TrySnapshot(filePath);
        }

        var required = TimeSpan.FromSeconds(stabilitySeconds);
        var timeout = Stopwatch.StartNew();
        var maxWait = TimeSpan.FromSeconds(Math.Max(30, stabilitySeconds * 5));
        var interval = TimeSpan.FromMilliseconds(Math.Clamp(stabilitySeconds * 250, 100, 500));

        FileSnapshot? previous = null;
        var stableSince = TimeSpan.Zero;

        while (timeout.Elapsed < maxWait)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = TrySnapshot(filePath);
            if (snapshot is null)
            {
                previous = null;
                stableSince = TimeSpan.Zero;
            }
            else if (previous is { } p && p == snapshot.Value)
            {
                stableSince += interval;
                if (stableSince >= required)
                {
                    return snapshot;
                }
            }
            else
            {
                previous = snapshot;
                stableSince = TimeSpan.Zero;
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    /// <summary>读取文件大小与最后写入时间；文件不存在或不可读时返回 null。</summary>
    public static FileSnapshot? TrySnapshot(string filePath)
    {
        try
        {
            var info = new FileInfo(filePath);
            if (!info.Exists)
            {
                return null;
            }

            return new FileSnapshot(info.Length, info.LastWriteTimeUtc);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool IsTemporaryName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        if (fileName.StartsWith("~$", StringComparison.Ordinal))
        {
            return true;
        }

        var lower = fileName.ToLowerInvariant();
        foreach (var suffix in TemporarySuffixes)
        {
            if (lower.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return lower.Contains(".tmp.") || lower.EndsWith(".tmp", StringComparison.Ordinal);
    }

    private static FileProblemInfo? ProbeAccess(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            Span<byte> probe = stackalloc byte[1];
            _ = stream.Read(probe);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return new FileProblemInfo(FileProblem.AccessDenied, "没有读取该文件的权限");
        }
        catch (IOException)
        {
            return new FileProblemInfo(FileProblem.InUse, "文件正被其他程序独占写入，等待完成后再试");
        }
        catch (Exception ex)
        {
            return new FileProblemInfo(FileProblem.InUse, "无法读取文件：" + ex.Message);
        }
    }
}
