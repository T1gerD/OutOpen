using System.IO;
using OutOpen.Core.Adapters;
using OutOpen.Core.Catalog;
using OutOpen.Core.Config;
using OutOpen.Core.Diagnostics;

namespace OutOpen.Core.Tests;

public class FolderResolverTests
{
    [Fact]
    public void ExpandTemplate_expands_wildcard_segments()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(Path.Combine(dir.Path, "alice", "files"));
        Directory.CreateDirectory(Path.Combine(dir.Path, "bob", "files"));

        var results = FolderResolver.ExpandTemplate(Path.Combine(dir.Path, "*", "files")).ToList();

        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.EndsWith(Path.Combine("alice", "files"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ResolveFolders_includes_extra_folders()
    {
        using var dir = new TempDir();
        var extra = Path.Combine(dir.Path, "extra");
        Directory.CreateDirectory(extra);
        var app = new SourceAppDefinition("test", "测试应用", "P0", [], [], [], "", false, "test");
        var settings = new SourceSettings { Enabled = true, ExtraFolders = [extra] };

        var folders = FolderResolver.ResolveFolders(app, settings);

        Assert.Contains(extra, folders);
    }

    [Fact]
    public void Missing_folders_are_ignored()
    {
        var app = new SourceAppDefinition("test", "测试应用", "P0", [], [@"C:\outopen-no-such-folder\files"], [], "", false, "test");

        var folders = FolderResolver.ResolveFolders(app, null);

        Assert.Empty(folders);
    }

    [Theory]
    [InlineData(@"C:\x\WeChat Files\a\FileStorage\File\report.docx", true)]
    [InlineData(@"C:\x\WeChat Files\a\FileStorage\File\report.docx.part", false)]
    [InlineData(@"C:\x\WeChat Files\a\FileStorage\File\picture.xyz", false)]
    [InlineData(@"C:\x\WeChat Files\a\FileStorage\Image\photo.png", false)]
    public void Candidate_detection_skips_temporaries_and_unsupported_types(string path, bool expected)
    {
        var excluded = new[] { @"\Image\", @"\Cache\" };

        Assert.Equal(expected, FolderResolver.IsCandidateFile(path, excluded));
    }

    [Fact]
    public void Excluded_segment_is_matched_case_insensitively()
    {
        Assert.True(FolderResolver.IsExcluded(@"C:\x\cache\a.docx", [@"\Cache\"]));
        Assert.False(FolderResolver.IsExcluded(@"C:\x\docs\a.docx", [@"\Cache\"]));
    }
}

public class PathMaskerTests
{
    [Fact]
    public void Full_profile_path_is_masked()
    {
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var masked = PathMasker.MaskPath(Path.Combine(profile, "Documents", "WeChat Files", "report.docx"));

        Assert.StartsWith("%USERPROFILE%", masked);
        Assert.DoesNotContain(profile, masked);
    }

    [Fact]
    public void Wxid_is_masked()
    {
        var masked = PathMasker.MaskPath(@"C:\Users\33502\Documents\WeChat Files\wxid_abc123\FileStorage\a.docx");

        Assert.Contains("wxid_***", masked);
        Assert.DoesNotContain("wxid_abc123", masked);
    }

    [Fact]
    public void Long_digit_runs_are_masked()
    {
        var masked = PathMasker.MaskPath(@"C:\Temp\file_12345678.docx");

        Assert.Contains("***", masked);
        Assert.DoesNotContain("12345678", masked);
    }

    [Fact]
    public void Empty_path_is_safe()
    {
        Assert.Equal(string.Empty, PathMasker.MaskPath(null));
        Assert.Equal(string.Empty, PathMasker.MaskDirectory(null));
    }
}

public class FileAppLogTests
{
    [Fact]
    public void Messages_go_to_daily_file()
    {
        using var dir = new TempDir();
        using (var log = new FileAppLog(dir.Path, retentionDays: 14))
        {
            log.Info("测试消息", "unit");
        }

        var file = Assert.Single(Directory.GetFiles(dir.Path, "outopen-*.log"));
        var content = File.ReadAllText(file);
        Assert.Contains("测试消息", content);
        Assert.Contains("[unit]", content);
    }
}
