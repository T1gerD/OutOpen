using OutOpen.Core.Diagnostics;
using OutOpen.Core.Models;
using OutOpen.Core.Hosting;

namespace OutOpen.Core.Tests;

public class OutOpenHostTests
{
    private static (OutOpenHost Host, FakeLauncher Launcher) CreateHost()
    {
        var dir = Path.Combine(Path.GetTempPath(), "outopen-host-tests", Guid.NewGuid().ToString("N"));
        var launcher = new FakeLauncher();
        var host = new OutOpenHost(
            dir,
            log: new ConsoleAppLog(),
            launcher: launcher,
            resolver: new FakeResolver());
        return (host, launcher);
    }

    [Fact]
    public void Manual_open_uses_pipeline()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (host, launcher) = CreateHost();
        using (host)
        {
            var result = host.HandleManual(path);

            Assert.Equal(InterceptStatus.Opened, result.Status);
            Assert.Equal(path, Assert.Single(launcher.DefaultOpened));
            Assert.NotEmpty(host.RecentResults);
            Assert.Equal(path, host.RecentResults[0].Request.FilePath);
        }
    }

    [Fact]
    public void Paused_host_does_not_open()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (host, launcher) = CreateHost();
        using (host)
        {
            host.SetPaused(true);
            var result = host.HandleManual(path);

            Assert.Equal(InterceptStatus.SkippedPaused, result.Status);
            Assert.Empty(launcher.Calls);
            Assert.True(host.Config.Paused);
        }
    }

    [Fact]
    public void Config_is_persisted_to_data_directory()
    {
        var (host, _) = CreateHost();
        using (host)
        {
            host.Config.Paused = true;
            host.SaveConfig();

            var configPath = Path.Combine(host.DataDirectory, "config.json");
            Assert.True(File.Exists(configPath));
            Assert.Contains("\"paused\": true", File.ReadAllText(configPath));
        }
    }

    [Fact]
    public void Sources_are_reported_with_states()
    {
        var (host, _) = CreateHost();
        using (host)
        {
            var sources = host.GetSources();

            Assert.Contains(sources, s => s.Id == "wechat" && s.Enabled);
            Assert.Contains(sources, s => s.Id == "qq" && s.Enabled);
            Assert.Contains(sources, s => s.Id == "dingtalk" && !s.Enabled);
            Assert.All(sources, s => Assert.False(string.IsNullOrWhiteSpace(s.DisplayName)));
        }
    }

    [Fact]
    public void Reload_picks_up_changed_config()
    {
        var (host, _) = CreateHost();
        using (host)
        {
            host.Config.Paused = true;
            host.SaveConfig();

            host.ReloadFromDisk();

            Assert.True(host.Config.Paused);
        }
    }
}
