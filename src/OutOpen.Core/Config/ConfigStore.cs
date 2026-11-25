using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using OutOpen.Core.Catalog;
using OutOpen.Core.Models;

namespace OutOpen.Core.Config;

/// <summary>配置文件读写：%LOCALAPPDATA%\OutOpen\config.json。损坏的配置会备份后回退为默认值。</summary>
public sealed class ConfigStore
{
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _path;

    public ConfigStore(string baseDirectory)
    {
        BaseDirectory = baseDirectory;
        _path = Path.Combine(baseDirectory, "config.json");
    }

    public string BaseDirectory { get; }

    public string ConfigPath => _path;

    /// <summary>读取配置；不存在时创建默认文件，损坏时重命名备份并重建。</summary>
    public OutOpenConfig LoadOrCreate()
    {
        if (!File.Exists(_path))
        {
            var created = ConfigDefaults.Create();
            Save(created);
            return created;
        }

        OutOpenConfig? config = null;
        try
        {
            var json = File.ReadAllText(_path);
            config = JsonSerializer.Deserialize<OutOpenConfig>(json, SerializerOptions);
        }
        catch (Exception)
        {
            BackupCorruptFile();
        }

        if (config is null)
        {
            config = ConfigDefaults.Create();
        }

        var changed = Normalize(config);
        if (changed)
        {
            Save(config);
        }

        return config;
    }

    /// <summary>规范化配置；返回是否发生了需要回写的更改。</summary>
    public static bool Normalize(OutOpenConfig config)
    {
        var changed = false;

        // v2：压缩包默认改为外部打开（此前为跳过），旧配置同步迁移一次。
        if (config.Version < 2 && config.Extensions is not null)
        {
            foreach (var (extension, settings) in config.Extensions)
            {
                if (settings.Action == OpenAction.Skip
                    && Catalog.SupportedTypes.Find(extension)?.Category == FileCategory.Archive)
                {
                    settings.Action = OpenAction.ExternalOpen;
                    changed = true;
                }
            }
        }

        if (config.Version != OutOpenConfig.CurrentVersion)
        {
            config.Version = OutOpenConfig.CurrentVersion;
            changed = true;
        }

        if (config.Settings is null)
        {
            config.Settings = new GeneralSettings();
            changed = true;
        }

        changed |= Clamp(config.Settings);

        config.Sources ??= new Dictionary<string, SourceSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in Catalog.SourceApps.All)
        {
            if (!config.Sources.ContainsKey(definition.Id))
            {
                config.Sources[definition.Id] = new SourceSettings { Enabled = definition.EnabledByDefault };
                changed = true;
            }
            else if (config.Sources[definition.Id].ExtraFolders is null)
            {
                config.Sources[definition.Id].ExtraFolders = [];
                changed = true;
            }
        }

        config.Extensions ??= new Dictionary<string, ExtensionSettings>(StringComparer.OrdinalIgnoreCase);

        // 键统一为小写带点号形式。
        var normalizedExtensions = new Dictionary<string, ExtensionSettings>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in config.Extensions)
        {
            var key = NormalizeExtension(pair.Key);
            if (key.Length == 0 || pair.Value is null)
            {
                changed = true;
                continue;
            }

            if (!string.Equals(pair.Key, key, StringComparison.Ordinal))
            {
                changed = true;
            }

            normalizedExtensions[key] = pair.Value;
        }

        foreach (var type in Catalog.SupportedTypes.All)
        {
            if (!normalizedExtensions.ContainsKey(type.Extension))
            {
                normalizedExtensions[type.Extension] = new ExtensionSettings
                {
                    Enabled = true,
                    Action = ConfigDefaults.DefaultActionFor(type.Category)
                };
                changed = true;
            }
        }

        config.Extensions = normalizedExtensions;

        config.Rules ??= [];
        foreach (var rule in config.Rules)
        {
            rule.Extensions = rule.Extensions?
                .Select(NormalizeExtension)
                .Where(e => e.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? [];
        }

        config.RecommendationStates ??= new Dictionary<string, RecommendationState>(StringComparer.OrdinalIgnoreCase);

        return changed;
    }

    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith('.') ? trimmed.ToLowerInvariant() : "." + trimmed.ToLowerInvariant();
    }

    public void Save(OutOpenConfig config)
    {
        Normalize(config);
        Directory.CreateDirectory(BaseDirectory);
        var json = JsonSerializer.Serialize(config, SerializerOptions);
        WriteAtomic(_path, json);
    }

    /// <summary>取扩展名配置，不存在时按支持清单补默认值（不自动落盘）。</summary>
    public static ExtensionSettings GetExtensionSettings(OutOpenConfig config, string extension)
    {
        var key = NormalizeExtension(extension);
        if (config.Extensions.TryGetValue(key, out var settings))
        {
            return settings;
        }

        var type = Catalog.SupportedTypes.Find(key);
        return new ExtensionSettings
        {
            Enabled = type is not null,
            Action = type is not null ? ConfigDefaults.DefaultActionFor(type.Category) : OpenAction.Skip
        };
    }

    private void BackupCorruptFile()
    {
        try
        {
            var backup = Path.Combine(
                BaseDirectory,
                $"config.invalid-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.Move(_path, backup, overwrite: true);
        }
        catch (Exception)
        {
            // 备份失败不应阻止程序启动。
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        try
        {
            File.Move(tmp, path, overwrite: true);
        }
        catch (IOException)
        {
            File.Copy(tmp, path, overwrite: true);
            try
            {
                File.Delete(tmp);
            }
            catch (IOException)
            {
            }
        }
    }

    private static bool Clamp(GeneralSettings settings)
    {
        var changed = false;

        changed |= Update(settings.StabilitySeconds, Math.Clamp(settings.StabilitySeconds, 0.0, 60.0), v => settings.StabilitySeconds = v);
        changed |= Update(settings.DedupeWindowSeconds, Math.Clamp(settings.DedupeWindowSeconds, 0, 600), v => settings.DedupeWindowSeconds = v);
        changed |= Update(settings.MaxFileSizeMb, Math.Clamp(settings.MaxFileSizeMb, 0, 102400), v => settings.MaxFileSizeMb = v);
        changed |= Update(settings.MaxArchiveSizeMb, Math.Clamp(settings.MaxArchiveSizeMb, 0, 102400), v => settings.MaxArchiveSizeMb = v);
        changed |= Update(settings.LogRetentionDays, Math.Clamp(settings.LogRetentionDays, 1, 365), v => settings.LogRetentionDays = v);

        return changed;

        static bool Update<T>(T current, T next, Action<T> set) where T : IEquatable<T>
        {
            if (current.Equals(next))
            {
                return false;
            }

            set(next);
            return true;
        }
    }
}
