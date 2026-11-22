using OutOpen.Core.Config;
using OutOpen.Core.Detection;
using OutOpen.Core.Launching;
using OutOpen.Core.Models;
using OutOpen.Core.Pipeline;
using OutOpen.Core.Rules;
using OutOpen.Core.Viewers;

namespace OutOpen.Core.Tests;

public class InterceptPipelineTests
{
    private static (InterceptPipeline Pipeline, FakeLauncher Launcher, FakeResolver Resolver) Create(
        DuplicateDetector? dedupe = null)
    {
        var launcher = new FakeLauncher();
        var resolver = new FakeResolver();
        var detector = new FileTypeDetector();
        var pipeline = new InterceptPipeline(
            detector,
            new FileValidationService(detector),
            new RulesEngine(),
            dedupe ?? new DuplicateDetector(8),
            launcher,
            new ViewerAdvisor(resolver, ViewerCatalog.CreateDefault()));
        return (pipeline, launcher, resolver);
    }

    private static OpenRequest Request(string path, string source = "wechat", OpenTrigger trigger = OpenTrigger.FolderArrival) => new()
    {
        FilePath = path,
        SourceAppId = source,
        SourceDisplayName = "微信 PC 版",
        Trigger = trigger
    };

    [Fact]
    public void Valid_document_is_opened_with_default_program()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (pipeline, launcher, _) = Create();
        var config = ConfigDefaults.Create();

        var result = pipeline.Handle(Request(path), config);

        Assert.Equal(InterceptStatus.Opened, result.Status);
        Assert.Equal(path, Assert.Single(launcher.DefaultOpened));
    }

    [Fact]
    public void Paused_config_skips_without_launching()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (pipeline, launcher, _) = Create();
        var config = ConfigDefaults.Create();
        config.Paused = true;

        var result = pipeline.Handle(Request(path), config);

        Assert.Equal(InterceptStatus.SkippedPaused, result.Status);
        Assert.Empty(launcher.Calls);
    }

    [Fact]
    public void Disabled_source_skips_automatic_events()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (pipeline, launcher, _) = Create();
        var config = ConfigDefaults.Create();
        config.Sources["wechat"].Enabled = false;

        var result = pipeline.Handle(Request(path), config);

        Assert.Equal(InterceptStatus.SkippedByRule, result.Status);
        Assert.Empty(launcher.Calls);
    }

    [Fact]
    public void Unsupported_type_is_skipped()
    {
        using var dir = new TempDir();
        var path = dir.Write("data.xyz", 1, 2, 3);
        var (pipeline, launcher, _) = Create();

        var result = pipeline.Handle(Request(path), ConfigDefaults.Create());

        Assert.Equal(InterceptStatus.SkippedUnsupportedType, result.Status);
        Assert.Empty(launcher.Calls);
    }

    [Fact]
    public void Duplicate_event_is_processed_once()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (pipeline, launcher, _) = Create();
        var config = ConfigDefaults.Create();

        var first = pipeline.Handle(Request(path), config);
        var second = pipeline.Handle(Request(path), config);

        Assert.Equal(InterceptStatus.Opened, first.Status);
        Assert.Equal(InterceptStatus.SkippedDuplicate, second.Status);
        Assert.Single(launcher.DefaultOpened);
    }

    [Fact]
    public void Empty_file_fails_validation()
    {
        using var dir = new TempDir();
        var path = dir.Write("empty.docx");
        var (pipeline, launcher, _) = Create();

        var result = pipeline.Handle(Request(path), ConfigDefaults.Create());

        Assert.Equal(InterceptStatus.ValidationFailed, result.Status);
        Assert.Empty(launcher.Calls);
    }

    [Fact]
    public void Missing_viewer_is_reported_for_markdown()
    {
        using var dir = new TempDir();
        var path = dir.WriteText("notes.md", "# hi");
        var (pipeline, launcher, resolver) = Create();
        resolver.Info = new DefaultProgramInfo(DefaultProgramStatus.NoAssociation, null, null, null);

        var result = pipeline.Handle(Request(path), ConfigDefaults.Create());

        Assert.Equal(InterceptStatus.ViewerMissing, result.Status);
        Assert.NotNull(result.Viewer);
        Assert.Equal("maxink", result.Viewer!.Primary?.Id);
        Assert.Empty(launcher.Calls);
    }

    [Fact]
    public void Failed_custom_program_falls_back_to_default()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (pipeline, launcher, _) = Create();
        launcher.ProgramSucceeds = false;
        var config = ConfigDefaults.Create();
        config.Extensions[".docx"].TargetProgram = @"C:\Tools\custom-viewer.exe";

        var result = pipeline.Handle(Request(path), config);

        Assert.Equal(InterceptStatus.OpenedWithFallback, result.Status);
        Assert.Equal(path, Assert.Single(launcher.DefaultOpened));
    }

    [Fact]
    public void Both_launch_attempts_failing_is_launch_failed()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (pipeline, launcher, _) = Create();
        launcher.DefaultSucceeds = false;
        launcher.ProgramSucceeds = false;
        var config = ConfigDefaults.Create();
        config.Extensions[".docx"].TargetProgram = @"C:\Tools\custom-viewer.exe";

        var result = pipeline.Handle(Request(path), config);

        Assert.Equal(InterceptStatus.LaunchFailed, result.Status);
    }

    [Fact]
    public void Default_program_failure_is_reported()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (pipeline, launcher, _) = Create();
        launcher.DefaultSucceeds = false;

        var result = pipeline.Handle(Request(path), ConfigDefaults.Create());

        Assert.Equal(InterceptStatus.LaunchFailed, result.Status);
        Assert.False(result.Launch!.Success);
    }

    [Fact]
    public void Manual_request_bypasses_disabled_source()
    {
        using var dir = new TempDir();
        var path = dir.Write("plan.docx", SampleFiles.Zip);
        var (pipeline, launcher, _) = Create();
        var config = ConfigDefaults.Create();
        config.Sources["wechat"].Enabled = false;

        var result = pipeline.Handle(Request(path, source: "manual", trigger: OpenTrigger.Manual), config);

        Assert.Equal(InterceptStatus.Opened, result.Status);
        Assert.Single(launcher.DefaultOpened);
    }
}
