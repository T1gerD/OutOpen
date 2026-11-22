using System.IO;

namespace OutOpen.Core.Rules;

/// <summary>短时间内的重复事件检测：同一文件在去抖窗口内重复到达时只处理一次。</summary>
public sealed class DuplicateDetector
{
    private readonly Dictionary<string, DateTimeOffset> _lastSeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _window;
    private readonly Func<DateTimeOffset> _clock;
    private readonly object _gate = new();

    public DuplicateDetector(int windowSeconds, Func<DateTimeOffset>? clock = null)
    {
        _window = TimeSpan.FromSeconds(Math.Max(0, windowSeconds));
        _clock = clock ?? (() => DateTimeOffset.UtcNow);
    }

    /// <summary>是否在去抖窗口内已经见过该文件；调用会同时记录本次时间。</summary>
    public bool IsDuplicate(string filePath)
    {
        if (_window <= TimeSpan.Zero)
        {
            return false;
        }

        var key = NormalizeKey(filePath);
        var now = _clock();

        lock (_gate)
        {
            if (_lastSeen.TryGetValue(key, out var last) && now - last < _window)
            {
                _lastSeen[key] = now;
                return true;
            }

            _lastSeen[key] = now;
            if (_lastSeen.Count > 512)
            {
                Prune(now);
            }

            return false;
        }
    }

    /// <summary>清理超过窗口的条目。</summary>
    public void Prune()
    {
        lock (_gate)
        {
            Prune(_clock());
        }
    }

    private void Prune(DateTimeOffset now)
    {
        var stale = _lastSeen.Where(pair => now - pair.Value >= _window).Select(pair => pair.Key).ToList();
        foreach (var key in stale)
        {
            _lastSeen.Remove(key);
        }
    }

    private static string NormalizeKey(string filePath)
    {
        try
        {
            return Path.GetFullPath(filePath);
        }
        catch (Exception)
        {
            return filePath;
        }
    }
}
