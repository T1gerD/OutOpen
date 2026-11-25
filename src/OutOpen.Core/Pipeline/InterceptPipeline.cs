using System.IO;
using OutOpen.Core.Config;
using OutOpen.Core.Detection;
using OutOpen.Core.Diagnostics;
using OutOpen.Core.Launching;
using OutOpen.Core.Models;
using OutOpen.Core.Rules;
using OutOpen.Core.Viewers;

namespace OutOpen.Core.Pipeline;

/// <summary>一次打开请求的完整处理链：去重 → 识别 → 规则 → 校验 → 查看器 → 启动 → 失败回退。</summary>
public sealed class InterceptPipeline
{
    private readonly FileTypeDetector _detector;
    private readonly FileValidationService _validator;
    private readonly RulesEngine _rules;
    private readonly DuplicateDetector _dedupe;
    private readonly IFileLauncher _launcher;
    private readonly ViewerAdvisor _viewerAdvisor;
    private readonly IAppLog? _log;

    public InterceptPipeline(
        FileTypeDetector detector,
        FileValidationService validator,
        RulesEngine rules,
        DuplicateDetector dedupe,
        IFileLauncher launcher,
        ViewerAdvisor viewerAdvisor,
        IAppLog? log = null)
    {
        _detector = detector;
        _validator = validator;
        _rules = rules;
        _dedupe = dedupe;
        _launcher = launcher;
        _viewerAdvisor = viewerAdvisor;
        _log = log;
    }

    public InterceptResult Handle(OpenRequest request, OutOpenConfig config)
    {
        try
        {
            if (config.Paused)
            {
                return Result(request, InterceptStatus.SkippedPaused, "OutOpen 已暂停接管，未打开文件");
            }

            var fileType = _detector.Detect(request.FilePath);
            if (!fileType.IsSupported)
            {
                return Result(request, InterceptStatus.SkippedUnsupportedType,
                    $"暂不支持 {fileType.Extension} 类型（识别为 {fileType.DisplayName}）", fileType: fileType);
            }

            if (_dedupe.IsDuplicate(request.NormalizedPath))
            {
                return Result(request, InterceptStatus.SkippedDuplicate,
                    "短时间内的重复事件，已忽略", fileType: fileType);
            }

            var decision = _rules.Decide(config, request, fileType);
            if (decision.Action == OpenAction.Skip)
            {
                return Result(request, InterceptStatus.SkippedByRule, decision.Reason, fileType: fileType, ruleReason: decision.Reason);
            }

            var options = new FileValidationOptions(
                config.Settings.StabilitySeconds,
                config.Settings.MaxFileSizeMb,
                config.Settings.RequireSignatureMatch,
                config.Settings.MaxArchiveSizeMb);

            var validation = _validator.Validate(request.FilePath, options);
            if (!validation.IsValid)
            {
                _log?.Warn($"校验失败：{Mask(request)} → {validation.Summary}", "pipeline");
                return Result(request, InterceptStatus.ValidationFailed, validation.Summary,
                    fileType: fileType, validation: validation, ruleReason: decision.Reason);
            }

            ViewerAssessment? viewer = null;
            var needsDefaultProgram = string.IsNullOrWhiteSpace(decision.TargetProgram);
            if (needsDefaultProgram)
            {
                viewer = _viewerAdvisor.Assess(config, request.Extension, fileType.MimeType);
                if (!viewer.HasViewer)
                {
                    _log?.Warn($"缺少可用查看器：{Mask(request)} → {viewer.StatusText}", "pipeline");
                    return Result(request, InterceptStatus.ViewerMissing,
                        viewer.StatusText + "，已给出查看器推荐", fileType: fileType, validation: validation,
                        ruleReason: decision.Reason, viewer: viewer);
                }
            }

            var launch = string.IsNullOrWhiteSpace(decision.TargetProgram)
                ? _launcher.OpenWithDefault(request.FilePath)
                : _launcher.OpenWithProgram(decision.TargetProgram!, request.FilePath);

            if (launch.Success)
            {
                _log?.Info($"已打开：{Mask(request)}（{DescribeLaunch(launch)}）", "pipeline");
                return Result(request, InterceptStatus.Opened, "已交给外部程序打开",
                    fileType: fileType, validation: validation, launch: launch,
                    ruleReason: decision.Reason, viewer: viewer, targetProgram: decision.TargetProgram);
            }

            // 指定程序失败时回退到系统默认程序；系统默认也失败时保留原始失败信息。
            if (!string.IsNullOrWhiteSpace(decision.TargetProgram))
            {
                var fallback = _launcher.OpenWithDefault(request.FilePath);
                if (fallback.Success)
                {
                    _log?.Warn($"指定程序启动失败，已回退系统默认程序：{Mask(request)}（{launch.ErrorMessage}）", "pipeline");
                    return Result(request, InterceptStatus.OpenedWithFallback,
                        $"指定的打开程序不可用，已回退到系统默认程序打开（原因：{launch.ErrorMessage}）",
                        fileType: fileType, validation: validation, launch: fallback,
                        ruleReason: decision.Reason, viewer: viewer, targetProgram: decision.TargetProgram);
                }

                return Result(request, InterceptStatus.LaunchFailed,
                    $"指定程序和系统默认程序均启动失败：{launch.ErrorMessage}；回退失败：{fallback.ErrorMessage}",
                    fileType: fileType, validation: validation, launch: launch,
                    ruleReason: decision.Reason, viewer: viewer, targetProgram: decision.TargetProgram);
            }

            _log?.Error($"启动失败：{Mask(request)} → {launch.ErrorMessage}", "pipeline");
            return Result(request, InterceptStatus.LaunchFailed,
                "外部程序启动失败：" + launch.ErrorMessage,
                fileType: fileType, validation: validation, launch: launch,
                ruleReason: decision.Reason, viewer: viewer, targetProgram: decision.TargetProgram);
        }
        catch (Exception ex)
        {
            _log?.Error($"处理异常：{Mask(request)} → {ex.Message}", "pipeline");
            return Result(request, InterceptStatus.Error, "处理文件时出现未预期异常：" + ex.Message);
        }
    }

    private static InterceptResult Result(
        OpenRequest request,
        InterceptStatus status,
        string message,
        FileTypeInfo? fileType = null,
        FileValidationReport? validation = null,
        LaunchResult? launch = null,
        ViewerAssessment? viewer = null,
        string? ruleReason = null,
        string? targetProgram = null) => new()
        {
            Status = status,
            Request = request,
            CompletedAt = DateTimeOffset.Now,
            Message = message,
            FileType = fileType ?? validation?.FileType,
            Validation = validation,
            Launch = launch,
            Viewer = viewer,
            RuleReason = ruleReason,
            TargetProgram = targetProgram
        };

    private static string Mask(OpenRequest request) =>
        $"{request.SourceAppId} {PathMasker.MaskPath(request.FilePath)}";

    private static string DescribeLaunch(LaunchResult launch)
    {
        if (launch.ProcessId is { } pid)
        {
            return $"pid {pid}" + (launch.ProgramPath is null ? "，系统默认程序" : "，" + Path.GetFileName(launch.ProgramPath));
        }

        return launch.ProgramPath is null ? "系统默认程序" : Path.GetFileName(launch.ProgramPath);
    }
}
