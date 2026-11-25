using OutOpen.Core.Detection;
using OutOpen.Core.Models;

namespace OutOpen.Core.Tests;

public class FileValidationServiceTests
{
    private readonly FileValidationService _validator = new(new FileTypeDetector());

    [Theory]
    [InlineData("report.docx.part")]
    [InlineData("report.docx.crdownload")]
    [InlineData("report.docx.tmp")]
    [InlineData("~$report.docx")]
    [InlineData("report.docx.dl")]
    public void Temporary_file_names_are_rejected(string name)
    {
        Assert.True(FileValidationService.IsTemporaryName(name));
    }

    [Theory]
    [InlineData("report.docx")]
    [InlineData("photo.jpg")]
    [InlineData("archive.zip")]
    public void Finished_file_names_pass(string name)
    {
        Assert.False(FileValidationService.IsTemporaryName(name));
    }

    [Fact]
    public void Missing_file_is_invalid()
    {
        var report = _validator.Validate(@"C:\definitely\missing\file.docx", new FileValidationOptions());

        Assert.False(report.IsValid);
        Assert.Contains(report.Problems, p => p.Problem == FileProblem.NotFound);
    }

    [Fact]
    public void Empty_file_is_invalid()
    {
        using var dir = new TempDir();
        var path = dir.Write("empty.docx");

        var report = _validator.Validate(path, new FileValidationOptions());

        Assert.False(report.IsValid);
        Assert.Contains(report.Problems, p => p.Problem == FileProblem.EmptyFile);
    }

    [Fact]
    public void Valid_document_passes_and_carries_type()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);

        var report = _validator.Validate(path, new FileValidationOptions());

        Assert.True(report.IsValid);
        Assert.NotNull(report.FileType);
        Assert.Equal(".docx", report.FileType!.Extension);
        Assert.True(report.SizeBytes > 0);
    }

    [Fact]
    public void Oversized_file_is_invalid()
    {
        using var dir = new TempDir();
        var path = dir.Write("big.pdf", MakeFile(2, 0x25, 0x50, 0x44, 0x46));

        var report = _validator.Validate(path, new FileValidationOptions(MaxFileSizeMb: 1));

        Assert.False(report.IsValid);
        Assert.Contains(report.Problems, p => p.Problem == FileProblem.TooLarge);
    }

    [Fact]
    public void Archive_uses_its_own_larger_limit()
    {
        using var dir = new TempDir();
        var path = dir.Write("big.zip", MakeZip(2));

        var report = _validator.Validate(path, new FileValidationOptions(MaxFileSizeMb: 1, MaxArchiveSizeMb: 4));

        Assert.True(report.IsValid);
    }

    [Fact]
    public void Archive_over_its_limit_is_invalid()
    {
        using var dir = new TempDir();
        var path = dir.Write("big.zip", MakeZip(2));

        var report = _validator.Validate(path, new FileValidationOptions(MaxFileSizeMb: 8, MaxArchiveSizeMb: 1));

        Assert.False(report.IsValid);
        Assert.Contains(report.Problems, p => p.Problem == FileProblem.TooLarge);
    }

    [Fact]
    public void Zero_archive_limit_means_unlimited()
    {
        using var dir = new TempDir();
        var path = dir.Write("big.zip", MakeZip(2));

        var report = _validator.Validate(path, new FileValidationOptions(MaxFileSizeMb: 1, MaxArchiveSizeMb: 0));

        Assert.True(report.IsValid);
    }

    [Fact]
    public void Zip_without_central_directory_is_incomplete()
    {
        using var dir = new TempDir();
        var path = dir.Write("partial.zip", SampleFiles.Zip);

        var report = _validator.Validate(path, new FileValidationOptions());

        Assert.False(report.IsValid);
        Assert.Contains(report.Problems, p => p.Problem == FileProblem.IncompleteArchive);
    }

    [Fact]
    public void Zip_with_end_record_passes()
    {
        using var dir = new TempDir();
        var path = dir.Write("complete.zip", MakeZip(1));

        var report = _validator.Validate(path, new FileValidationOptions());

        Assert.True(report.IsValid);
    }

    private static byte[] MakeFile(int sizeMb, params byte[] header)
    {
        var bytes = new byte[sizeMb * 1024 * 1024];
        header.CopyTo(bytes, 0);
        return bytes;
    }

    private static byte[] MakeZip(int sizeMb)
    {
        var bytes = new byte[sizeMb * 1024 * 1024];
        bytes[0] = 0x50;
        bytes[1] = 0x4B;
        bytes[2] = 0x03;
        bytes[3] = 0x04;
        var tail = bytes.Length - 22;
        bytes[tail] = 0x50;
        bytes[tail + 1] = 0x4B;
        bytes[tail + 2] = 0x05;
        bytes[tail + 3] = 0x06;
        return bytes;
    }

    [Fact]
    public void Signature_mismatch_fails_when_required()
    {
        using var dir = new TempDir();
        var path = dir.Write("fake.pdf", SampleFiles.Png);

        var report = _validator.Validate(path, new FileValidationOptions(RequireSignatureMatch: true));

        Assert.False(report.IsValid);
        Assert.Contains(report.Problems, p => p.Problem == FileProblem.SignatureMismatch);
    }

    [Fact]
    public async Task Stable_file_becomes_stable()
    {
        using var dir = new TempDir();
        var path = dir.Write("stable.docx", SampleFiles.Zip);

        var snapshot = await _validator.WaitUntilStableAsync(path, 0.2, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Equal(new FileInfo(path).Length, snapshot!.Value.SizeBytes);
    }
}
