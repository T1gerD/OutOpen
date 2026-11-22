using OutOpen.Core.Config;
using OutOpen.Core.Detection;
using OutOpen.Core.Models;
using OutOpen.Core.Rules;

namespace OutOpen.Core.Tests;

public class RulesEngineTests
{
    private readonly RulesEngine _rules = new();

    private static OpenRequest Request(string source = "wechat", OpenTrigger trigger = OpenTrigger.FolderArrival, string filePath = @"C:\Temp\report.docx") => new()
    {
        FilePath = filePath,
        SourceAppId = source,
        SourceDisplayName = source,
        Trigger = trigger
    };

    private static FileTypeInfo Docx() =>
        new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileCategory.Document, "Word 文档", "ZIP/OOXML", true, true);

    [Fact]
    public void Paused_config_skips_everything()
    {
        var config = ConfigDefaults.Create();
        config.Paused = true;

        var decision = _rules.Decide(config, Request(), Docx());

        Assert.Equal(OpenAction.Skip, decision.Action);
        Assert.Equal("paused", decision.RuleId);
    }

    [Fact]
    public void Automatic_open_can_be_turned_off()
    {
        var config = ConfigDefaults.Create();
        config.Settings.AutoOpenOnArrival = false;

        var decision = _rules.Decide(config, Request(), Docx());

        Assert.Equal(OpenAction.Skip, decision.Action);
        Assert.Equal("auto-open-off", decision.RuleId);
    }

    [Fact]
    public void Disabled_source_blocks_folder_arrival_but_not_manual()
    {
        var config = ConfigDefaults.Create();
        config.Sources["wechat"].Enabled = false;

        var automatic = _rules.Decide(config, Request(), Docx());
        var manual = _rules.Decide(config, Request(trigger: OpenTrigger.Manual), Docx());

        Assert.Equal(OpenAction.Skip, automatic.Action);
        Assert.Equal("source-disabled", automatic.RuleId);
        Assert.Equal(OpenAction.ExternalOpen, manual.Action);
    }

    [Fact]
    public void Disabled_extension_skips()
    {
        var config = ConfigDefaults.Create();
        config.Extensions[".docx"].Enabled = false;

        var decision = _rules.Decide(config, Request(), Docx());

        Assert.Equal(OpenAction.Skip, decision.Action);
        Assert.Equal("extension-disabled", decision.RuleId);
    }

    [Fact]
    public void Images_are_skipped_by_default()
    {
        var config = ConfigDefaults.Create();
        var jpg = new FileTypeInfo(".jpg", "image/jpeg", FileCategory.Image, "JPEG 图片", "JPEG", true, true);

        var decision = _rules.Decide(config, Request(filePath: @"C:\Temp\photo.jpg"), jpg);

        Assert.Equal(OpenAction.Skip, decision.Action);
        Assert.Equal("extension-policy", decision.RuleId);
    }

    [Fact]
    public void Highest_priority_rule_wins()
    {
        var config = ConfigDefaults.Create();
        config.Rules.Add(new RuleSettings
        {
            Id = "low",
            Extensions = [".docx"],
            Action = OpenAction.ExternalOpen,
            TargetProgram = @"C:\low.exe",
            Priority = 1
        });
        config.Rules.Add(new RuleSettings
        {
            Id = "high",
            SourceAppId = "wechat",
            Extensions = [".docx"],
            Action = OpenAction.Skip,
            Priority = 10
        });

        var decision = _rules.Decide(config, Request(), Docx());

        Assert.Equal(OpenAction.Skip, decision.Action);
        Assert.Equal("high", decision.RuleId);
    }

    [Fact]
    public void Source_specific_rule_does_not_match_other_source()
    {
        var config = ConfigDefaults.Create();
        config.Rules.Add(new RuleSettings
        {
            Id = "qq-only",
            SourceAppId = "qq",
            Extensions = [".docx"],
            Action = OpenAction.Skip,
            Priority = 10
        });

        var decision = _rules.Decide(config, Request("wechat"), Docx());

        Assert.Equal(OpenAction.ExternalOpen, decision.Action);
    }

    [Fact]
    public void Unsupported_type_is_skipped()
    {
        var config = ConfigDefaults.Create();

        var decision = _rules.Decide(config, Request(filePath: @"C:\Temp\data.xyz"), null);

        Assert.Equal(OpenAction.Skip, decision.Action);
        Assert.Equal("unsupported-type", decision.RuleId);
    }

    [Fact]
    public void Custom_extension_policy_is_used()
    {
        var config = ConfigDefaults.Create();
        config.Extensions[".docx"].TargetProgram = @"C:\Tools\viewer.exe";

        var decision = _rules.Decide(config, Request(), Docx());

        Assert.Equal(OpenAction.ExternalOpen, decision.Action);
        Assert.Equal(@"C:\Tools\viewer.exe", decision.TargetProgram);
    }
}
