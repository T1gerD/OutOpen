using OutOpen.Core.Detection;
using OutOpen.Core.Models;

namespace OutOpen.Core.Tests;

public class FileTypeDetectorTests
{
    private readonly FileTypeDetector _detector = new();

    [Fact]
    public void Pdf_signature_is_detected()
    {
        using var dir = new TempDir();
        var path = dir.Write("report.pdf", SampleFiles.Pdf);

        var info = _detector.Detect(path);

        Assert.Equal(".pdf", info.Extension);
        Assert.Equal("application/pdf", info.MimeType);
        Assert.Equal(FileCategory.Document, info.Category);
        Assert.True(info.IsSupported);
        Assert.True(info.SignatureMatchesExtension);
        Assert.Contains("PDF", info.Signature, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Docx_zip_signature_is_detected()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);

        var info = _detector.Detect(path);

        Assert.Equal(".docx", info.Extension);
        Assert.True(info.SignatureMatchesExtension);
        Assert.True(info.IsSupported);
    }

    [Fact]
    public void Png_and_jpeg_are_detected()
    {
        using var dir = new TempDir();
        var png = _detector.Detect(dir.Write("a.png", SampleFiles.Png));
        var jpeg = _detector.Detect(dir.Write("b.jpg", SampleFiles.Jpeg));

        Assert.Equal(FileCategory.Image, png.Category);
        Assert.Equal(FileCategory.Image, jpeg.Category);
        Assert.True(png.SignatureMatchesExtension);
        Assert.True(jpeg.SignatureMatchesExtension);
    }

    [Fact]
    public void Text_file_without_signature_still_matches_extension()
    {
        using var dir = new TempDir();
        var path = dir.WriteText("notes.md", "# 标题\n\n正文");

        var info = _detector.Detect(path);

        Assert.Equal(".md", info.Extension);
        Assert.Equal("text/markdown", info.MimeType);
        Assert.True(info.SignatureMatchesExtension);
    }

    [Fact]
    public void Mismatched_signature_is_reported()
    {
        using var dir = new TempDir();
        var path = dir.Write("fake.pdf", SampleFiles.Png);

        var info = _detector.Detect(path);

        Assert.Equal(".pdf", info.Extension);
        Assert.False(info.SignatureMatchesExtension);
    }

    [Fact]
    public void Unknown_extension_is_unsupported()
    {
        using var dir = new TempDir();
        var path = dir.Write("data.xyz", 1, 2, 3, 4, 5);

        var info = _detector.Detect(path);

        Assert.False(info.IsSupported);
        Assert.Equal(FileCategory.Other, info.Category);
    }
}
