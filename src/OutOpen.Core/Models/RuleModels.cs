namespace OutOpen.Core.Models;

/// <summary>对一次打开请求的处理策略。</summary>
public enum OpenAction
{
    /// <summary>调用用户配置的程序或系统默认关联应用（外部打开）。</summary>
    ExternalOpen,

    /// <summary>交给 Windows 默认程序 / "打开方式" 对话框。</summary>
    SystemDefault,

    /// <summary>不接管，保留来源应用的原始行为。</summary>
    Skip
}

/// <summary>规则引擎给出的决策。</summary>
public sealed record RuleDecision(
    OpenAction Action,
    string? TargetProgram,
    string Reason,
    string? RuleId = null)
{
    public static RuleDecision Skip(string reason, string? ruleId = null) => new(OpenAction.Skip, null, reason, ruleId);

    public static RuleDecision Open(string? targetProgram, string reason, string? ruleId = null) =>
        new(OpenAction.ExternalOpen, targetProgram, reason, ruleId);
}
