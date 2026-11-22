using OutOpen.Core.Config;
using OutOpen.Core.Launching;
using OutOpen.Core.Viewers;

namespace OutOpen.Core.Tests;

public class ViewerAdvisorTests
{
    private static ViewerAdvisor Create(FakeResolver resolver) =>
        new(resolver, ViewerCatalog.CreateDefault());

    [Fact]
    public void Missing_association_recommends_maxink_for_markdown()
    {
        var resolver = new FakeResolver
        {
            Info = new DefaultProgramInfo(DefaultProgramStatus.NoAssociation, null, null, null)
        };
        var advisor = Create(resolver);
        var config = ConfigDefaults.Create();

        var assessment = advisor.Assess(config, ".md");

        Assert.False(assessment.HasViewer);
        Assert.Equal(ViewerStatusKind.NoAssociation, assessment.Status);
        Assert.NotNull(assessment.Primary);
        Assert.Equal("maxink", assessment.Primary!.Id);
        Assert.Contains("maxink", assessment.Primary.DownloadUrl!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(assessment.AllRecommendations, r => r.Id == "obsidian");
    }

    [Fact]
    public void Available_viewer_means_no_primary_recommendation()
    {
        var resolver = new FakeResolver();
        var advisor = Create(resolver);
        var config = ConfigDefaults.Create();

        var assessment = advisor.Assess(config, ".pdf");

        Assert.True(assessment.HasViewer);
        Assert.Equal(ViewerStatusKind.Available, assessment.Status);
        Assert.Null(assessment.Primary);
        Assert.NotEmpty(assessment.Alternatives);
    }

    [Fact]
    public void Broken_association_recommends_alternative()
    {
        var resolver = new FakeResolver
        {
            Info = new DefaultProgramInfo(DefaultProgramStatus.Broken, @"C:\gone\viewer.exe", "已失效程序", null)
        };
        var advisor = Create(resolver);
        var config = ConfigDefaults.Create();

        var assessment = advisor.Assess(config, ".pdf");

        Assert.False(assessment.HasViewer);
        Assert.NotNull(assessment.Primary);
    }

    [Fact]
    public void Ignored_extension_stays_suppressed()
    {
        var resolver = new FakeResolver
        {
            Info = new DefaultProgramInfo(DefaultProgramStatus.NoAssociation, null, null, null)
        };
        var advisor = Create(resolver);
        var config = ConfigDefaults.Create();

        advisor.Ignore(config, ".md");
        var assessment = advisor.Assess(config, ".md");

        Assert.True(assessment.Suppressed);
        Assert.Null(assessment.Primary);
    }

    [Fact]
    public void Snoozed_extension_is_suppressed_until_expiry()
    {
        var clock = DateTimeOffset.UtcNow;
        var resolver = new FakeResolver
        {
            Info = new DefaultProgramInfo(DefaultProgramStatus.NoAssociation, null, null, null)
        };
        var advisor = new ViewerAdvisor(resolver, ViewerCatalog.CreateDefault(), () => clock);
        var config = ConfigDefaults.Create();

        advisor.Snooze(config, ".md", TimeSpan.FromHours(1));
        var now = advisor.Assess(config, ".md");

        clock = clock.AddHours(2);
        var later = advisor.Assess(config, ".md");

        Assert.True(now.Suppressed);
        Assert.NotNull(now.SnoozeUntil);
        Assert.False(later.Suppressed);
        Assert.NotNull(later.Primary);
    }

    [Fact]
    public void Reset_clears_state()
    {
        var resolver = new FakeResolver
        {
            Info = new DefaultProgramInfo(DefaultProgramStatus.NoAssociation, null, null, null)
        };
        var advisor = Create(resolver);
        var config = ConfigDefaults.Create();
        advisor.Ignore(config, ".md");

        advisor.Reset(config, ".md");

        Assert.False(advisor.Assess(config, ".md").Suppressed);
    }

    [Fact]
    public void Extension_normalization_accepts_uppercase_and_missing_dot()
    {
        Assert.Equal(".md", ViewerAdvisor.Normalize("MD"));
        Assert.Equal(".md", ViewerAdvisor.Normalize(".MD"));
        Assert.Equal(string.Empty, ViewerAdvisor.Normalize(""));
    }
}
