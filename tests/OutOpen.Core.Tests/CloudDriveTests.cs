using System;
using System.IO;
using System.Linq;
using OutOpen.Core.Adapters;
using OutOpen.Core.Catalog;
using OutOpen.Core.Config;
using OutOpen.Core.Detection;
using Xunit;

namespace OutOpen.Core.Tests;

public sealed class CloudDriveTests
{
    private static readonly string[] CloudIds =
    [
        "baidunetdisk", "aliyundrive", "quark", "pan115", "weiyun", "nutstore", "onedrive"
    ];

    [Fact]
    public void Cloud_drives_are_part_of_the_catalog_with_watch_roots()
    {
        foreach (var id in CloudIds)
        {
            var app = SourceApps.Find(id);
            Assert.NotNull(app);
            Assert.False(string.IsNullOrWhiteSpace(app!.DisplayName));
            Assert.NotEmpty(app.WatchRoots);
            Assert.False(app.EnabledByDefault, $"{id} 应默认关闭，避免误接管");
        }
    }

    [Fact]
    public void Every_source_except_telegram_has_a_watch_root_or_extra_folder_flow()
    {
        foreach (var app in SourceApps.All)
        {
            if (app.Id == "telegram" || app.Id == "manual")
            {
                continue;
            }

            Assert.NotEmpty(app.WatchRoots);
        }
    }

    [Theory]
    [InlineData("movie.mp4.downloading")]
    [InlineData("报告.pdf.baiduyun.uploading.cfg")]
    [InlineData("data.zip.qdl")]
    [InlineData("video.mkv.part")]
    [InlineData("file.bin.tmp")]
    public void Cloud_download_placeholders_are_temporary(string name)
    {
        Assert.True(FileValidationService.IsTemporaryName(name), name + " 应被视为下载中文件");
    }

    [Fact]
    public void Extra_folders_are_resolved_and_deduplicated()
    {
        using var temp = new TempDir();
        var app = SourceApps.Find("quark")!;
        var settings = new SourceSettings
        {
            Enabled = true,
            ExtraFolders = [temp.Path, temp.Path.ToUpperInvariant()]
        };

        var folders = FolderResolver.ResolveFolders(app, settings);

        Assert.Equal(1, folders.Count(f => string.Equals(f, temp.Path, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Extra_folders_round_trip_through_config()
    {
        using var temp = new TempDir();
        var store = new ConfigStore(temp.Path);
        var config = ConfigDefaults.Create();
        config.Sources["baidunetdisk"] = new SourceSettings
        {
            Enabled = true,
            ExtraFolders = [@"D:\Downloads\百度网盘"]
        };

        store.Save(config);
        var loaded = store.LoadOrCreate();

        var restored = Assert.Single(loaded.Sources["baidunetdisk"].ExtraFolders);
        Assert.Equal(@"D:\Downloads\百度网盘", restored);
        Assert.True(loaded.Sources["baidunetdisk"].Enabled);
    }

    [Fact]
    public void Cloud_drive_exclusions_cover_thumbnail_caches()
    {
        var quark = SourceApps.Find("quark")!;
        Assert.Contains(quark.ExcludedSegments, s => s.Contains("thumbnails", StringComparison.OrdinalIgnoreCase));

        var baidu = SourceApps.Find("baidunetdisk")!;
        Assert.Contains(baidu.ExcludedSegments, s => s.Contains("baiduyun.uploading", StringComparison.OrdinalIgnoreCase));
    }
}
