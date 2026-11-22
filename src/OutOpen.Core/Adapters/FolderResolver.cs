using System.IO;
using OutOpen.Core.Catalog;
using OutOpen.Core.Config;
using OutOpen.Core.Detection;

namespace OutOpen.Core.Adapters;

/// <summary>把来源应用定义的目录模板（含环境变量与 * 通配）解析为真实目录。</summary>
public static class FolderResolver
{
    /// <summary>解析某个来源应用应监听的目录列表。</summary>
    public static IReadOnlyList<string> ResolveFolders(SourceAppDefinition app, SourceSettings? settings)
    {
        var templates = new List<string>(app.WatchRoots);
        if (settings?.ExtraFolders is { Count: > 0 } extra)
        {
            templates.AddRange(extra);
        }

        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var template in templates)
        {
            foreach (var folder in ExpandTemplate(template))
            {
                if (seen.Add(folder))
                {
                    result.Add(folder);
                }
            }
        }

        return result;
    }

    /// <summary>展开环境变量与 * 通配段，只返回真实存在的目录。</summary>
    public static IEnumerable<string> ExpandTemplate(string template)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            yield break;
        }

        string expanded;
        try
        {
            expanded = Environment.ExpandEnvironmentVariables(template.Trim().Trim('"'));
        }
        catch (Exception)
        {
            yield break;
        }

        if (!expanded.Contains('*'))
        {
            if (Directory.Exists(expanded))
            {
                yield return Path.GetFullPath(expanded);
            }

            yield break;
        }

        var segments = expanded.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            yield break;
        }

        var current = new List<string> { segments[0] + "\\" };

        for (var i = 1; i < segments.Length; i++)
        {
            var segment = segments[i];
            var next = new List<string>();

            foreach (var prefix in current)
            {
                if (segment == "*")
                {
                    try
                    {
                        next.AddRange(Directory.EnumerateDirectories(prefix).Select(Path.GetFullPath));
                    }
                    catch (Exception)
                    {
                        // 无权限或目录不存在时跳过该分支。
                    }
                }
                else if (segment.Contains('*'))
                {
                    try
                    {
                        next.AddRange(Directory.EnumerateDirectories(prefix, segment).Select(Path.GetFullPath));
                    }
                    catch (Exception)
                    {
                    }
                }
                else
                {
                    var combined = Path.Combine(prefix, segment);
                    if (Directory.Exists(combined))
                    {
                        next.Add(Path.GetFullPath(combined));
                    }
                }
            }

            current = next;
            if (current.Count == 0)
            {
                yield break;
            }
        }

        foreach (var folder in current)
        {
            yield return folder;
        }
    }

    /// <summary>路径中是否包含需要排除的片段（缩略图、缓存等）。</summary>
    public static bool IsExcluded(string filePath, IReadOnlyList<string> excludedSegments)
    {
        foreach (var segment in excludedSegments)
        {
            if (!string.IsNullOrEmpty(segment)
                && filePath.Contains(segment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>是否是可接管的候选文件：排除临时文件与不支持的类型。</summary>
    public static bool IsCandidateFile(string filePath, IReadOnlyList<string> excludedSegments)
    {
        if (FolderResolver.IsExcluded(filePath, excludedSegments))
        {
            return false;
        }

        var name = Path.GetFileName(filePath);
        if (FileValidationService.IsTemporaryName(name))
        {
            return false;
        }

        return SupportedTypes.IsSupported(Path.GetExtension(filePath));
    }
}
