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

internal static class SmartBpOcrContactSheetMapper
{
    /// <summary>
    /// 按区域映射拆分 OCR 文本行，并转成区域局部坐标。
    /// </summary>
    /// <param name="result">OCR Provider返回的整图识别结果。</param>
    /// <param name="regions">拼接图区域映射。</param>
    /// <param name="unmappedLineCount">未命中任何区域的文本行数量。</param>
    /// <returns>按区域分组的 OCR 文本。</returns>
    public static IReadOnlyList<SmartBpOcrRegionText> MapLinesToRegions(
        OcrTextBlockResult result,
        IReadOnlyList<SmartBpOcrContactSheetRegion> regions,
        out int unmappedLineCount)
    {
        var grouped = regions.ToDictionary(
            item => item.Region,
            _ => new List<OcrTextLine>());
        unmappedLineCount = 0;

        foreach (var line in result.Lines)
        {
            var mapping = regions.FirstOrDefault(region =>
                line.CenterX >= region.SheetRect.X &&
                line.CenterX <= region.SheetRect.X + region.SheetRect.Width &&
                line.CenterY >= region.SheetRect.Y &&
                line.CenterY <= region.SheetRect.Y + region.SheetRect.Height);
            if (mapping == null)
            {
                unmappedLineCount++;
                continue;
            }

            grouped[mapping.Region].Add(ToRegionLocalLine(line, mapping.SheetRect));
        }

        return grouped
            .Select(item => new SmartBpOcrRegionText
            {
                Region = item.Key,
                Lines = item.Value.OrderBy(line => line.CenterY).ThenBy(line => line.CenterX).ToArray()
            })
            .ToArray();
    }

    /// <summary>
    /// 将拼接图坐标中的文本行转换为区域局部坐标。
    /// </summary>
    /// <param name="line">OCR 文本行。</param>
    /// <param name="sheetRect">区域在拼接图中的位置。</param>
    /// <returns>区域局部坐标文本行。</returns>
    private static OcrTextLine ToRegionLocalLine(OcrTextLine line, Rect sheetRect)
    {
        var clipped = line.BoundingBox & sheetRect;
        return line with
        {
            BoundingBox = new Rect(
                clipped.X - sheetRect.X,
                clipped.Y - sheetRect.Y,
                clipped.Width,
                clipped.Height),
            CenterX = line.CenterX - sheetRect.X,
            CenterY = line.CenterY - sheetRect.Y
        };
    }
}

/// <summary>
/// 将 OCR 文本解析为阵营内角色候选。
/// </summary>
internal sealed partial class SmartBpOcrTextResolver(ICharacterSelectionService characterSelectionService) : ISmartBpOcrTextResolver
{
    /// <summary>
    /// 在指定阵营和槽位语境下解析 OCR 文本。
    /// </summary>
    /// <param name="text">OCR 原始文本。</param>
    /// <param name="camp">目标阵营。</param>
    /// <param name="slotIndex">目标槽位索引。</param>
    /// <param name="provider">OCR Provider名称。</param>
    /// <returns>规范化角色解析结果。</returns>
    public SmartBpNormalizedCharacter ResolveCharacterFromLine(string text, Camp camp, int slotIndex, string? provider = null)
    {
        if (SmartBpBusinessStateParser.IsUnselected(text))
            return new(text, null, camp, slotIndex, 1, [], "unselected", false, "unselected slot");
        if (IsStatusOrPhaseText(text))
            return new(text, null, camp, slotIndex, 0, [], "filtered-status", false, "status or phase text");
        var result = characterSelectionService.ResolveCharacterDetailed(text, camp);
        var resolved = result.CanonicalName ?? "unresolved";
        var diagnostic = $"raw={text}; provider={provider ?? "unknown"}; camp={camp}; result={resolved}; matchMode={result.MatchMode}; score={result.Score:0.00}; safe={result.IsAutoApplySafe}; reason={result.Reason}";
        return new(
            text,
            result.CanonicalName,
            camp,
            slotIndex,
            result.Score,
            [diagnostic],
            result.MatchMode,
            result.IsAutoApplySafe,
            result.Reason);
    }

    /// <summary>
    /// 判断文本是否更像阶段/状态提示，而不是角色名。
    /// </summary>
    /// <param name="text">OCR 文本。</param>
    /// <returns>是状态文本则返回 <see langword="true"/>。</returns>
    private static bool IsStatusOrPhaseText(string text)
    {
        var normalized = NormalizeForMatch(text);
        string[] markers = ["等待中", "屏蔽求生者", "屏蔽监管者", "禁用求生者", "禁用监管者", "选择求生者", "选择监管者", "求生者选择角色中", "选择天赋中", "天赋已锁定"];
        return markers.Any(marker => normalized.Contains(NormalizeForMatch(marker), StringComparison.Ordinal));
    }

    /// <summary>
    /// 规范化 OCR 文本，执行 Unicode 兼容归一化并去除首尾空白。
    /// </summary>
    /// <param name="value">原始文本。</param>
    /// <returns>规范化文本。</returns>
    internal static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Normalize(NormalizationForm.FormKC).Trim();

    /// <summary>
    /// 生成用于模糊匹配的规范化文本。
    /// </summary>
    /// <param name="value">原始文本。</param>
    /// <returns>去除符号、空白并转大写后的文本。</returns>
    internal static string NormalizeForMatch(string? value)
    {
        var normalized = NormalizeText(value);
        normalized = StripDecorativeQuotes(normalized);
        return NonWordRegex().Replace(normalized, "").ToUpperInvariant();
    }

    /// <summary>
    /// 去掉 OCR 结果外围可能误带的成对装饰引号。
    /// </summary>
    /// <param name="value">输入文本。</param>
    /// <returns>去除外围引号后的文本。</returns>
    internal static string StripDecorativeQuotes(string value)
    {
        var trimmed = value.Trim();
        var changed = true;
        while (changed && trimmed.Length >= 2)
        {
            changed = false;
            foreach (var (left, right) in QuotePairs)
            {
                if (trimmed[0] != left || trimmed[^1] != right) continue;
                trimmed = trimmed[1..^1].Trim();
                changed = true;
                break;
            }
        }

        return trimmed;
    }

    private static readonly (char Left, char Right)[] QuotePairs =
    [
        ('"', '"'), ('“', '”'), ('”', '“'), ('『', '』'), ('「', '」'), ('《', '》'), ('〈', '〉'), ('‘', '’'), ('\'', '\'')
    ];

    [GeneratedRegex(@"[\s\p{P}\p{S}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonWordRegex();
}

/// <summary>
/// 基于顶部阶段区域 OCR 文本判断当前 BP 阶段。
/// </summary>
