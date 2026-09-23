using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media.Imaging;
using neo_bpsys_wpf.Core.Abstractions.Services;
using neo_bpsys_wpf.Core.Enums;
using neo_bpsys_wpf.Core.Models;
using neo_bpsys_wpf.SmartBp.Module.Abstractions;
using neo_bpsys_wpf.SmartBp.Module.Models.Recognition;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace neo_bpsys_wpf.SmartBp.Module.Services.Recognition;

internal static class SmartBpOcrPhaseClassifier
{
    /// <summary>
    /// 根据 OCR 文本行和区域宽度分类当前阶段。
    /// </summary>
    /// <param name="lines">阶段区域 OCR 文本行。</param>
    /// <param name="phaseRegionWidth">阶段区域宽度，用于区分左右侧提示。</param>
    /// <param name="diagnostics">诊断日志收集器。</param>
    /// <returns>阶段识别结果。</returns>
    public static SmartBpPhaseRecognitionResult Classify(
        IReadOnlyList<OcrTextLine> lines,
        double phaseRegionWidth,
        ICollection<string> diagnostics)
    {
        foreach (var line in lines)
            diagnostics.Add($"phase line: provider={line.Provider ?? "unknown"}; coordinateSpace=region-local; text={line.Text}; bbox={line.BoundingBox}; x={line.CenterX:0.0}; y={line.CenterY:0.0}; conf={line.Confidence:0.00}");

        var phaseText = string.Join('\n', lines.Select(line => line.Text));
        if (lines.Any(line => ContainsNormalized(line.Text, "天赋已锁定")))
            return Matched("天赋已锁定", "matched rule: any line contains 天赋已锁定", diagnostics);

        var left = lines.Where(line => IsLeft(line, phaseRegionWidth)).ToArray();
        var right = lines.Where(line => !IsLeft(line, phaseRegionWidth)).ToArray();

        if (right.Any(line => ContainsBanSur(line.Text)))
            return Matched("屏蔽求生者", "matched rule: right-side line contains 屏蔽求生者", diagnostics);
        if (left.Any(line => ContainsBanHun(line.Text)))
            return Matched("屏蔽监管者", "matched rule: left-side line contains 屏蔽监管者", diagnostics);
        if (left.Any(line => ContainsNormalized(line.Text, "求生者选择角色中")))
            return Matched("求生者选择角色中", "matched rule: left-side line contains 求生者选择角色中", diagnostics);
        if (left.Any(line => ContainsNormalized(line.Text, "选择求生者")))
            return Matched("选择求生者", "matched rule: left-side line contains 选择求生者", diagnostics);
        if (right.Any(line => ContainsNormalized(line.Text, "选择监管者")))
            return Matched("选择监管者", "matched rule: right-side line contains 选择监管者", diagnostics);
        if (left.Any(line => ContainsNormalized(line.Text, "选择天赋中")))
            return Matched("求生者选择天赋中", "matched rule: left-side line contains 选择天赋中", diagnostics);
        if (right.Any(line => ContainsNormalized(line.Text, "选择天赋中")))
            return Matched("监管者选择天赋中", "matched rule: right-side line contains 选择天赋中", diagnostics);
        if (lines.Any(line => ContainsNormalized(line.Text, "等待中")))
            return Matched("等待中", "matched rule: only waiting text found", diagnostics);

        return Matched("未知", "matched rule: no phase text matched", diagnostics);
    }

    /// <summary>
    /// 创建阶段匹配结果并追加诊断信息。
    /// </summary>
    /// <param name="phase">识别到的阶段。</param>
    /// <param name="message">匹配规则说明。</param>
    /// <param name="diagnostics">诊断日志收集器。</param>
    /// <returns>阶段识别结果。</returns>
    private static SmartBpPhaseRecognitionResult Matched(string phase, string message, ICollection<string> diagnostics)
    {
        diagnostics.Add(message);
        diagnostics.Add($"final phase: {phase}");
        return new() { Phase = phase };
    }

    /// <summary>
    /// 判断文本行中心是否位于阶段区域左半边。
    /// </summary>
    /// <param name="line">OCR 文本行。</param>
    /// <param name="width">阶段区域宽度。</param>
    /// <returns>位于左半边返回 <see langword="true"/>。</returns>
    private static bool IsLeft(OcrTextLine line, double width) => line.CenterX < width * .5;

    private static bool ContainsBanSur(string text) =>
        ContainsNormalized(text, "屏蔽求生者") || ContainsNormalized(text, "禁用求生者");

    private static bool ContainsBanHun(string text) =>
        ContainsNormalized(text, "屏蔽监管者") || ContainsNormalized(text, "禁用监管者");

    private static bool ContainsNormalized(string text, string candidate) =>
        SmartBpOcrTextResolver.NormalizeForMatch(text).Contains(SmartBpOcrTextResolver.NormalizeForMatch(candidate), StringComparison.Ordinal);
}

/// <summary>
/// 根据 OCR 文本判断角色 BP 后的区域选择、等待开局等状态。
/// </summary>
