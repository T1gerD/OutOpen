using System.Text.Json;
using System.Text.Json.Nodes;
using OutOpen.Core.Config;
using OutOpen.Core.Models;

namespace OutOpen.Core.Tests;

public class ConfigStoreTests
{
    [Fact]
    public void Default_config_has_sources_and_extensions()
    {
        using var dir = new TempDir();
        var store = new ConfigStore(dir.Path);

        var config = store.LoadOrCreate();

        Assert.Equal(OutOpenConfig.CurrentVersion, config.Version);
        Assert.True(config.Sources.ContainsKey("wechat"));
        Assert.True(config.Sources.ContainsKey("qq"));
        Assert.True(config.Extensions.ContainsKey(".pdf"));
        Assert.True(config.Extensions.ContainsKey(".md"));
        Assert.Equal(OpenAction.ExternalOpen, config.Extensions[".md"].Action);
        Assert.Equal(OpenAction.Skip, config.Extensions[".jpg"].Action);
        Assert.True(File.Exists(store.ConfigPath));
    }

    [Fact]
    public void Saved_config_round_trips()
    {
        using var dir = new TempDir();
        var store = new ConfigStore(dir.Path);
        var config = store.LoadOrCreate();
        config.Paused = true;
        config.Settings.DedupeWindowSeconds = 30;
        config.Sources["wechat"].Enabled = false;

        store.Save(config);

        var reloaded = new ConfigStore(dir.Path).LoadOrCreate();

        Assert.True(reloaded.Paused);
        Assert.Equal(30, reloaded.Settings.DedupeWindowSeconds);
        Assert.False(reloaded.Sources["wechat"].Enabled);
    }

    [Fact]
    public void Extension_keys_are_normalized_on_save()
    {
        using var dir = new TempDir();
        var store = new ConfigStore(dir.Path);
        var config = store.LoadOrCreate();
        config.Extensions["PDF"] = new ExtensionSettings { Action = OpenAction.Skip };

        store.Save(config);

        var json = JsonNode.Parse(File.ReadAllText(store.ConfigPath))!;
        var extensions = json["extensions"]!.AsObject();

        Assert.True(extensions.ContainsKey(".pdf"));
        Assert.False(extensions.ContainsKey("PDF"));
    }

    [Fact]
    public void Corrupt_config_is_backed_up_and_defaults_returned()
    {
        using var dir = new TempDir();
        var store = new ConfigStore(dir.Path);
        File.WriteAllText(store.ConfigPath, "{ this is not json");

        var config = store.LoadOrCreate();

        Assert.NotNull(config);
        Assert.True(config.Sources.ContainsKey("wechat"));
        Assert.Single(Directory.GetFiles(dir.Path, "config.invalid-*.json"));
    }

    [Fact]
    public void Out_of_range_values_are_clamped()
    {
        using var dir = new TempDir();
        var store = new ConfigStore(dir.Path);
        var config = store.LoadOrCreate();
        config.Settings.MaxFileSizeMb = 999_999;
        config.Settings.MaxArchiveSizeMb = -3;
        config.Settings.DedupeWindowSeconds = -5;
        config.Settings.StabilitySeconds = 600;

        store.Save(config);
        var reloaded = new ConfigStore(dir.Path).LoadOrCreate();

        Assert.Equal(102400, reloaded.Settings.MaxFileSizeMb);
        Assert.Equal(0, reloaded.Settings.MaxArchiveSizeMb);
        Assert.Equal(0, reloaded.Settings.DedupeWindowSeconds);
        Assert.Equal(60, reloaded.Settings.StabilitySeconds);
    }

    [Fact]
    public void Version1_config_migrates_archives_to_external_open()
    {
        using var dir = new TempDir();
        var store = new ConfigStore(dir.Path);
        var defaults = ConfigDefaults.Create();
        defaults.Version = 1;
        defaults.Extensions[".zip"].Action = Core.Models.OpenAction.Skip;
        store.Save(defaults);

        var reloaded = new ConfigStore(dir.Path).LoadOrCreate();

        Assert.Equal(OutOpenConfig.CurrentVersion, reloaded.Version);
        Assert.Equal(Core.Models.OpenAction.ExternalOpen, reloaded.Extensions[".zip"].Action);
    }

    [Fact]
    public void Config_with_extra_fields_still_loads()
    {
        using var dir = new TempDir();
        var store = new ConfigStore(dir.Path);
        var defaults = ConfigDefaults.Create();
        var json = JsonSerializer.Serialize(defaults, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var node = JsonNode.Parse(json)!.AsObject();
        node["futureField"] = "ignored";
        File.WriteAllText(store.ConfigPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var loaded = store.LoadOrCreate();

        Assert.True(loaded.Sources.Count >= 7);
    }
}
