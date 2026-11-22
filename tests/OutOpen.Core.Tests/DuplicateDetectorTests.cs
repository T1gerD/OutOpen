using OutOpen.Core.Rules;

namespace OutOpen.Core.Tests;

public class DuplicateDetectorTests
{
    [Fact]
    public void Repeated_event_inside_window_is_duplicate()
    {
        var now = DateTimeOffset.UtcNow;
        var detector = new DuplicateDetector(5, () => now);

        Assert.False(detector.IsDuplicate(@"C:\Temp\a.docx"));
        now = now.AddSeconds(2);
        Assert.True(detector.IsDuplicate(@"C:\Temp\a.docx"));
    }

    [Fact]
    public void Event_after_window_is_not_duplicate()
    {
        var now = DateTimeOffset.UtcNow;
        var detector = new DuplicateDetector(5, () => now);

        Assert.False(detector.IsDuplicate(@"C:\Temp\a.docx"));
        now = now.AddSeconds(6);
        Assert.False(detector.IsDuplicate(@"C:\Temp\a.docx"));
    }

    [Fact]
    public void Different_paths_are_independent()
    {
        var now = DateTimeOffset.UtcNow;
        var detector = new DuplicateDetector(5, () => now);

        Assert.False(detector.IsDuplicate(@"C:\Temp\a.docx"));
        Assert.False(detector.IsDuplicate(@"C:\Temp\b.docx"));
    }

    [Fact]
    public void Zero_window_disables_deduplication()
    {
        var now = DateTimeOffset.UtcNow;
        var detector = new DuplicateDetector(0, () => now);

        detector.IsDuplicate(@"C:\Temp\a.docx");

        Assert.False(detector.IsDuplicate(@"C:\Temp\a.docx"));
    }

    [Fact]
    public void Path_comparison_is_case_insensitive()
    {
        var now = DateTimeOffset.UtcNow;
        var detector = new DuplicateDetector(5, () => now);

        detector.IsDuplicate(@"C:\Temp\A.DOCX");

        Assert.True(detector.IsDuplicate(@"c:\temp\a.docx"));
    }
}
