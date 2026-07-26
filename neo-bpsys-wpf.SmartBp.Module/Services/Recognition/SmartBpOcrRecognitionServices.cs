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

/// <summary>
/// 将多个 BP 识别区域裁剪并纵向拼接成一张 OCR 拼接图。
/// </summary>
internal sealed class SmartBpOcrContactSheetBuilder(ISmartBpRecognitionFrameCropper cropper) : ISmartBpOcrContactSheetBuilder
{
    private const int Padding = 24;

    /// <summary>
    /// 构建 OCR 拼接图，并记录每个原始区域在拼接图中的位置映射。
    /// </summary>
    /// <param name="frame">完整捕获帧。</param>
    /// <param name="regions">需要识别的区域集合。</param>
    /// <returns>拼接图和区域映射信息。</returns>
    public SmartBpOcrContactSheet Build(BitmapSource frame, IReadOnlyList<SmartBpRecognitionRegion> regions)
    {
        var distinctRegions = regions.Distinct().ToArray();
        if (distinctRegions.Length == 0)
            return new(new Mat(new Size(1, 1), MatType.CV_8UC3, Scalar.All(255)), []);

        var crops = new List<(SmartBpRecognitionRegion Region, SmartBpCroppedFrame Crop, Mat Image)>();
        try
        {
            foreach (var region in distinctRegions)
            {
                var crop = cropper.CropWithInfo(frame, region);
                using var raw = BitmapSourceConverter.ToMat(crop.Image);
                crops.Add((region, crop, ToBgr(raw)));
            }

            // 合并识别可以减少 OCR Provider调用次数；映射表负责把结果再还原到区域局部坐标。
            var width = crops.Max(item => item.Image.Width);
            var height = crops.Sum(item => item.Image.Height) + Padding * Math.Max(0, crops.Count - 1);
            var sheet = new Mat(new Size(width, height), MatType.CV_8UC3, Scalar.All(255));
            var mappings = new List<SmartBpOcrContactSheetRegion>(crops.Count);
            var y = 0;
            foreach (var (region, crop, image) in crops)
            {
                using var target = new Mat(sheet, new Rect(0, y, image.Width, image.Height));
                image.CopyTo(target);
                mappings.Add(new(
                    region,
                    new Rect(0, y, image.Width, image.Height),
                    new Rect(crop.X, crop.Y, crop.Width, crop.Height)));
                y += image.Height + Padding;
            }

            return new(sheet, mappings);
        }
        finally
        {
            foreach (var item in crops)
                item.Image.Dispose();
        }
    }

    /// <summary>
    /// 把 OpenCV 图像规范化为 BGR 三通道，方便后续拼接和 OCR Provider处理。
    /// </summary>
    /// <param name="source">源图像。</param>
    /// <returns>BGR 三通道图像，调用方负责释放。</returns>
    private static Mat ToBgr(Mat source)
    {
        var result = new Mat();
        if (source.Channels() == 1)
            Cv2.CvtColor(source, result, ColorConversionCodes.GRAY2BGR);
        else if (source.Channels() == 4)
            Cv2.CvtColor(source, result, ColorConversionCodes.BGRA2BGR);
        else
            source.CopyTo(result);
        return result;
    }
}

/// <summary>
/// 将拼接图上的 OCR 文本行映射回各 BP 识别区域。
/// </summary>
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
            return new(text, null, null, camp, slotIndex, 1, [], "unselected", false, "unselected slot");
        if (IsStatusOrPhaseText(text))
            return new(text, null, null, camp, slotIndex, 0, [], "filtered-status", false, "status or phase text");
        var result = characterSelectionService.ResolveCharacterDetailed(text, camp);
        var resolved = result.CanonicalName ?? "unresolved";
        var diagnostic = $"raw={text}; provider={provider ?? "unknown"}; camp={camp}; result={resolved}; matchMode={result.MatchMode}; score={result.Score:0.00}; safe={result.IsAutoApplySafe}; reason={result.Reason}";
        return new(
            text,
            result.CanonicalName,
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
internal static class SmartBpPostBpStatusDetector
{
    /// <summary>
    /// 从 OCR 文本行中检测角色 BP 后状态。
    /// </summary>
    /// <param name="lines">待检测的 OCR 文本行。</param>
    /// <returns>检测结果。</returns>
    public static SmartBpPostBpStatusResult Detect(IReadOnlyList<OcrTextLine> lines)
    {
        var rawLines = lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var evidence = string.Join(" / ", rawLines);
        var normalizedLines = rawLines.Select(SmartBpOcrTextResolver.NormalizeForMatch).Where(text => text.Length > 0).ToArray();
        var combined = string.Concat(normalizedLines);
        var auxiliary = new List<string>();
        var hasRemainingSeconds = ContainsFuzzy(combined, "剩余", .5) && combined.Contains('秒');
        if (hasRemainingSeconds) auxiliary.Add("剩余秒");
        var hasGoTo = ContainsFuzzy(combined, "前往", .5);
        var hasBracketedDestination = rawLines.Any(line => Regex.IsMatch(
            line.Normalize(NormalizationForm.FormKC),
            @"[【\[（(《<].+?[】\]）)》>]",
            RegexOptions.CultureInvariant));
        if (hasGoTo && hasBracketedDestination) auxiliary.Add("前往地图");

        var titleMatch = PrimaryTitles
            .Select(candidate => new
            {
                Candidate = candidate,
                Exact = combined.Contains(candidate.Phase, StringComparison.Ordinal),
                TokenMatch = candidate.Tokens.All(token => combined.Contains(token, StringComparison.Ordinal)),
                Similarity = BestSubstringSimilarity(combined, candidate.Phase)
            })
            .Where(match => match.Exact || match.TokenMatch || match.Similarity >= .75)
            .OrderByDescending(match => match.Exact)
            .ThenByDescending(match => match.TokenMatch)
            .ThenByDescending(match => match.Similarity)
            .FirstOrDefault();
        if (titleMatch != null)
        {
            var score = titleMatch.Exact ? 1 : titleMatch.TokenMatch ? .9 : titleMatch.Similarity;
            if (auxiliary.Count > 0)
                score = Math.Min(1, score + .03 * auxiliary.Count);
            var matchMode = titleMatch.Exact ? "exact" : titleMatch.TokenMatch ? "keywords" : "edit-distance";
            return Match(titleMatch.Candidate.Phase, titleMatch.Candidate.Scene, $"{matchMode} title anchor", evidence, combined, auxiliary, score);
        }

        if (hasRemainingSeconds && hasGoTo && hasBracketedDestination)
            return Match("等待游戏开始", SmartBpRecognitionScene.WaitingGameStart,
                "combined auxiliary countdown and destination anchors", evidence, combined, auxiliary, .7);

        return new SmartBpPostBpStatusResult
        {
            Evidence = evidence,
            NormalizedText = combined,
            AuxiliaryEvidence = auxiliary
        };
    }

    private static readonly (string Phase, SmartBpRecognitionScene Scene, string[] Tokens)[] PrimaryTitles =
    [
        ("求生者选择区域中", SmartBpRecognitionScene.AreaSelectionSurvivor, ["求生者", "选择", "区域"]),
        ("监管者选择区域中", SmartBpRecognitionScene.AreaSelectionHunter, ["监管者", "选择", "区域"]),
        ("等待游戏开始", SmartBpRecognitionScene.WaitingGameStart, ["等待", "游戏", "开始"])
    ];

    private static SmartBpPostBpStatusResult Match(
        string phase,
        SmartBpRecognitionScene scene,
        string reason,
        string evidence,
        string normalized,
        IReadOnlyList<string> auxiliary,
        double score) =>
        new()
        {
            IsPostBp = true,
            Phase = phase,
            Scene = scene,
            Reason = reason,
            Evidence = evidence,
            NormalizedText = normalized,
            AuxiliaryEvidence = auxiliary,
            Score = score
        };

    private static bool ContainsFuzzy(string text, string candidate, double threshold) =>
        BestSubstringSimilarity(text, SmartBpOcrTextResolver.NormalizeForMatch(candidate)) >= threshold;

    private static double BestSubstringSimilarity(string text, string candidate)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(candidate)) return 0;
        if (text.Contains(candidate, StringComparison.Ordinal)) return 1;
        var best = 0d;
        var minLength = Math.Max(1, candidate.Length - 1);
        var maxLength = Math.Min(text.Length, candidate.Length + 1);
        for (var length = minLength; length <= maxLength; length++)
        for (var start = 0; start + length <= text.Length; start++)
        {
            var distance = EditDistance(text.AsSpan(start, length), candidate.AsSpan());
            best = Math.Max(best, 1d - (double)distance / Math.Max(length, candidate.Length));
        }
        return best;
    }

    private static int EditDistance(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}

/// <summary>
/// 将单个 OCR 区域文本解析为 SmartBP 聚焦业务识别结果。
/// </summary>
internal sealed class SmartBpOcrRegionParser(ISmartBpOcrTextResolver resolver)
{
    public SmartBpOcrParsedRegionResult ParseDetailed(
        SmartBpRecognitionRegion region,
        IReadOnlyList<OcrTextLine> lines,
        SmartBpOcrFieldParseContext? parseContext = null)
    {
        var diagnostics = new List<string>();
        foreach (var line in lines.Where(line => IsStatusLine(line.Text)))
            diagnostics.Add($"ocr-ignore region={SmartBpOcrBpRecognitionService.ToRegionId(region)} raw={line.Text} provider={line.Provider ?? "unknown"} confidence={line.Confidence:0.00} reason=status-line");
        var result = Parse(region, lines, diagnostics, parseContext);
        IReadOnlyList<SmartBpRecognizedPlayerCharacterSlot> slots = result.PickedHun != null ? [result.PickedHun] : result.Slots;
        var unresolved = slots.Count == 0 || slots.Any(slot => SmartBpBusinessStateParser.IsUnselected(slot.CharacterName));
        var safe = slots.Where(slot => !SmartBpBusinessStateParser.IsUnselected(slot.CharacterName)).All(slot => slot.IsAutoApplySafe);
        diagnostics.Add($"region={SmartBpOcrBpRecognitionService.ToRegionId(region)}; criticalUnresolved={unresolved}; autoApplySafe={safe}");
        return new()
        {
            Result = result,
            Diagnostics = diagnostics,
            HasCriticalUnresolvedField = unresolved,
            IsAutoApplySafe = safe
        };
    }

    public SmartBpFocusedBusinessExtractionResult Parse(
        SmartBpRecognitionRegion region,
        IReadOnlyList<OcrTextLine> lines,
        ICollection<string> diagnostics,
        SmartBpOcrFieldParseContext? parseContext = null)
    {
        return region switch
        {
            SmartBpRecognitionRegion.RightTop => ParseBanRegion(lines, Camp.Sur, "banned_sur", 4, diagnostics),
            SmartBpRecognitionRegion.LeftTop => ParseBanRegion(lines, Camp.Hun, "banned_hun", 2, diagnostics),
            SmartBpRecognitionRegion.LeftBottom => ParseSurvivorPickRegion(lines, diagnostics, parseContext),
            SmartBpRecognitionRegion.RightBottom => ParseHunterPickRegion(lines, diagnostics),
            _ => new SmartBpFocusedBusinessExtractionResult()
        };
    }

    private SmartBpFocusedBusinessExtractionResult ParseBanRegion(
        IReadOnlyList<OcrTextLine> lines,
        Camp camp,
        string field,
        int count,
        ICollection<string> diagnostics)
    {
        var regionId = field == "banned_sur" ? "right_top" : "left_top";
        var candidates = lines.Where(line => !IsStatusLine(line.Text))
            .Select(line => new { Line = line, Character = resolver.ResolveCharacterFromLine(line.Text, camp, -1, line.Provider) })
            .ToArray();
        foreach (var candidate in candidates)
            AddResolverDiagnostics(diagnostics, regionId, candidate.Line, candidate.Character);
        var matches = candidates
            .Where(item => item.Character.ResolvedCharacterKey != null)
            .GroupBy(item => item.Character.ResolvedCharacterKey, StringComparer.Ordinal)
            .Select(group => group.OrderByDescending(item => item.Character.Confidence).First())
            .OrderBy(item => item.Line.CenterX)
            .ThenBy(item => item.Line.CenterY)
            .Take(count)
            .ToArray();
        var slots = DefaultPlayerSlots(count);
        for (var i = 0; i < matches.Length; i++)
        {
            slots[i].CharacterName = matches[i].Character.ResolvedCharacterKey!;
            ApplyRecognitionMetadata(slots[i], matches[i].Character);
        }
        diagnostics.Add($"{field}: parsed [{string.Join(", ", slots.Select(slot => $"{slot.Index}={slot.CharacterName}"))}]");
        return new() { Phase = "未知", TargetField = field, Slots = slots };
    }

    private SmartBpFocusedBusinessExtractionResult ParseSurvivorPickRegion(
        IReadOnlyList<OcrTextLine> lines,
        ICollection<string> diagnostics,
        SmartBpOcrFieldParseContext? parseContext = null)
    {
        var slots = DefaultPlayerSlots(4);
        var layoutLines = lines
            .Where(line => !IsStatusLine(line.Text))
            .Select(line => CreateLayoutLine(SmartBpRecognitionRegion.LeftBottom, line))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .ToArray();
        var rows = ClusterRows(layoutLines, CalculateRowTolerance(layoutLines));
        var mode = parseContext?.ResolvePickedSurParseMode() ?? SmartBpPickedSurOcrParseMode.Unknown;
        diagnostics.Add($"picked_sur parse mode={mode}");

        AddPickedSurRowDiagnostics(rows, diagnostics);

        if (mode == SmartBpPickedSurOcrParseMode.Unknown)
        {
            // 未知模式回退到旧行为：物理行索引语义。
            ParseSurvivorPickRegionLegacy(rows, slots, diagnostics);
            return new() { Phase = "未知", TargetField = "picked_sur", Slots = slots };
        }

        // 结构化行分类：噪声 / character / player-id / talent。
        var (xMin, xMax) = ResolveXRange(layoutLines);
        var scoredRows = rows.Select((row, index) => ScoreRow(row, index, xMin, xMax)).ToArray();
        AddRowClassificationDiagnostics(scoredRows, diagnostics);

        var nonNoiseRows = scoredRows.Where(sr => sr.Classification != RowClassification.Noise).ToArray();
        if (nonNoiseRows.Length == 0)
        {
            diagnostics.Add("picked_sur: all rows classified as noise; no slots parsed.");
            diagnostics.Add($"picked_sur: parsed [{string.Join(", ", slots.Select(slot => $"{slot.Index}={slot.CharacterName}/{slot.PlayerId ?? "null"}"))}]");
            return new() { Phase = "未知", TargetField = "picked_sur", Slots = slots };
        }

        // 选择 character row：优先选择 slot-like character texts 最多的行。
        var characterRowScored = nonNoiseRows
            .Where(sr => sr.Classification == RowClassification.Character || sr.Features.HasFourSlotStructure)
            .OrderByDescending(sr => sr.Features.ValidSurvivorCharacterCount + sr.Features.UnselectedCount)
            .ThenByDescending(sr => sr.Features.CoveredSlotsCount)
            .ThenBy(sr => sr.PhysicalIndex)
            .FirstOrDefault() ?? nonNoiseRows.First();

        // 选择 player-id row：character row 之后的第一个 player-id-like 行。
        var playerRowScored = nonNoiseRows
            .Where(sr => sr.PhysicalIndex > characterRowScored.PhysicalIndex)
            .Where(sr => sr.Classification == RowClassification.PlayerId || sr.Features.PlayerIdLikeCount > 0)
            .OrderByDescending(sr => sr.Features.CoveredSlotsCount)
            .ThenBy(sr => sr.PhysicalIndex)
            .FirstOrDefault();

        diagnostics.Add($"picked_sur selected character row={characterRowScored.PhysicalIndex}; player-id row={playerRowScored?.PhysicalIndex ?? -1}");

        // 分配 character slots。
        var slotCenters = BuildSlotCentersFromXRange(xMin, xMax, slots.Count);
        var characterItems = characterRowScored.Lines.OrderBy(line => line.CenterX).ToArray();
        var assignedCharacterSlots = new HashSet<int>();
        foreach (var item in characterItems)
        {
            if (assignedCharacterSlots.Count >= slots.Count)
                break;
            var slotIndex = ResolveSurvivorSlotIndex(item.CenterX, xMin, xMax, slots.Count);
            if (assignedCharacterSlots.Contains(slotIndex))
                slotIndex = Enumerable.Range(0, slots.Count)
                    .Where(index => !assignedCharacterSlots.Contains(index))
                    .OrderBy(index => Math.Abs(item.CenterX - slotCenters[index]))
                    .First();
            ApplyPickedSurCharacterSlot(slots[slotIndex], item, diagnostics);
            assignedCharacterSlots.Add(slotIndex);
        }

        // 分配 player IDs。
        if (playerRowScored != null)
            AssignPickedSurPlayerIdsBySlot(slots, slotCenters, playerRowScored.Lines, xMin, xMax, diagnostics);

        // 在 DistributeChara / SurvivorTalent 模式下，输出 talent/extra 行忽略诊断。
        if (mode is SmartBpPickedSurOcrParseMode.DistributeChara or SmartBpPickedSurOcrParseMode.SurvivorTalent)
            AddIgnoredPickedSurRowDiagnostics(scoredRows, characterRowScored, playerRowScored, diagnostics);

        diagnostics.Add("picked_sur slot assignment:");
        foreach (var slot in slots)
            diagnostics.Add($"slot {slot.Index} char={slot.CharacterName} player_id={slot.PlayerId ?? "null"}");
        diagnostics.Add($"picked_sur: parsed [{string.Join(", ", slots.Select(slot => $"{slot.Index}={slot.CharacterName}/{slot.PlayerId ?? "null"}"))}]");
        return new() { Phase = "未知", TargetField = "picked_sur", Slots = slots };
    }

    /// <summary>未知模式下的旧行为回退：物理行索引语义。</summary>
    private void ParseSurvivorPickRegionLegacy(
        IReadOnlyList<IReadOnlyList<OcrLineLayout>> rows,
        IReadOnlyList<SmartBpRecognizedPlayerCharacterSlot> slots,
        ICollection<string> diagnostics)
    {
        var characterRow = rows.FirstOrDefault() ?? [];
        var playerRow = rows.Skip(1).FirstOrDefault() ?? [];
        var selectedCharacterItems = characterRow.Take(4).ToArray();
        var slotCenters = BuildPickedSurSlotCenters(selectedCharacterItems, playerRow, slots.Count);
        if (selectedCharacterItems.Length >= slots.Count)
        {
            for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
                ApplyPickedSurCharacterSlot(slots[slotIndex], selectedCharacterItems[slotIndex], diagnostics);
        }
        else
        {
            var assignedCharacterSlots = new HashSet<int>();
            foreach (var item in selectedCharacterItems.OrderBy(line => line.CenterX))
            {
                var slotIndex = Enumerable.Range(0, slots.Count)
                    .Where(index => !assignedCharacterSlots.Contains(index))
                    .OrderBy(index => Math.Abs(item.CenterX - slotCenters[index]))
                    .First();
                ApplyPickedSurCharacterSlot(slots[slotIndex], item, diagnostics);
                assignedCharacterSlots.Add(slotIndex);
            }
        }
        AssignPickedSurPlayerIds(slots, slotCenters, playerRow);
        AddIgnoredPickedSurRowDiagnostics(rows, diagnostics);
    }

    /// <summary>基于 X 范围将中心坐标映射到 survivor 槽位索引（0-3）。</summary>
    private static int ResolveSurvivorSlotIndex(double centerX, double xMin, double xMax, int slotCount)
    {
        if (xMax <= xMin)
            return 0;
        var normalized = (centerX - xMin) / (xMax - xMin);
        var slotIndex = (int)Math.Round(normalized * (slotCount - 1));
        return Math.Clamp(slotIndex, 0, slotCount - 1);
    }

    /// <summary>从 X 范围构建 4 个槽位中心坐标。</summary>
    private static double[] BuildSlotCentersFromXRange(double xMin, double xMax, int slotCount)
    {
        var centers = new double[slotCount];
        if (xMax <= xMin)
        {
            for (var i = 0; i < slotCount; i++)
                centers[i] = xMin + i * 100;
            return centers;
        }
        for (var i = 0; i < slotCount; i++)
            centers[i] = xMin + (double)i / (slotCount - 1) * (xMax - xMin);
        return centers;
    }

    /// <summary>计算所有 layout lines 的 X 范围。</summary>
    private static (double Min, double Max) ResolveXRange(IReadOnlyList<OcrLineLayout> lines)
    {
        if (lines.Count == 0)
            return (0, 1);
        var minX = lines.Min(line => line.CenterX);
        var maxX = lines.Max(line => line.CenterX);
        return (minX, maxX);
    }

    /// <summary>对一行 OCR layout lines 进行结构化评分。</summary>
    private ScoredRow ScoreRow(IReadOnlyList<OcrLineLayout> row, int physicalIndex, double xMin, double xMax)
    {
        var features = new RowFeatures
        {
            ItemCount = row.Count,
            CoveredSlotsCount = row.Select(line => ResolveSurvivorSlotIndex(line.CenterX, xMin, xMax, 4)).Distinct().Count(),
            AverageConfidence = row.Count > 0 ? row.Average(line => line.Confidence) : 0,
            ValidSurvivorCharacterCount = 0,
            UnselectedCount = 0,
            PlayerIdLikeCount = 0,
            ShortFragmentCount = 0
        };

        foreach (var line in row)
        {
            if (SmartBpBusinessStateParser.IsUnselected(line.Text))
            {
                features.UnselectedCount++;
                continue;
            }
            var resolved = resolver.ResolveCharacterFromLine(line.Text, Camp.Sur, -1, line.Provider);
            if (resolved.ResolvedCharacterKey != null)
            {
                features.ValidSurvivorCharacterCount++;
                continue;
            }
            if (line.Text.Length <= 2)
                features.ShortFragmentCount++;
            if (!IsInvalidPlayerId(line.Text) && !string.IsNullOrWhiteSpace(line.Text))
                features.PlayerIdLikeCount++;
        }

        features.HasFourSlotStructure = features.CoveredSlotsCount >= 3;
        features.HasMostlySlotLikeTexts = (features.UnselectedCount + features.ValidSurvivorCharacterCount) * 2 >= features.ItemCount;
        features.IsSingleLowValueFragmentRow = features.ItemCount == 1 && features.ValidSurvivorCharacterCount == 0 && features.UnselectedCount == 0;

        var classification = ClassifyRow(features);
        return new ScoredRow(physicalIndex, row, features, classification);
    }

    /// <summary>基于结构特征对行进行分类。</summary>
    private static RowClassification ClassifyRow(RowFeatures features)
    {
        // 噪声行：低覆盖、无角色、无未选择、不构成 player-id 行结构。
        if (features.IsSingleLowValueFragmentRow && features.CoveredSlotsCount <= 1 && features.AverageConfidence < 0.6)
            return RowClassification.Noise;
        if (features.CoveredSlotsCount <= 1 && features.ValidSurvivorCharacterCount == 0 && features.UnselectedCount == 0 && features.PlayerIdLikeCount == 0)
            return RowClassification.Noise;

        // character 行：有未选择或有效 survivor 角色。
        if (features.UnselectedCount > 0 || features.ValidSurvivorCharacterCount > 0)
            return RowClassification.Character;

        // player-id 行：非角色、非未选择文本，覆盖多槽位。
        if (features.PlayerIdLikeCount > 0 && features.ValidSurvivorCharacterCount == 0 && features.UnselectedCount == 0)
            return RowClassification.PlayerId;

        return RowClassification.Unknown;
    }

    /// <summary>按 X 坐标和槽位中心分配 player IDs。</summary>
    private void AssignPickedSurPlayerIdsBySlot(
        IReadOnlyList<SmartBpRecognizedPlayerCharacterSlot> slots,
        IReadOnlyList<double> slotCenters,
        IReadOnlyList<OcrLineLayout> playerRow,
        double xMin,
        double xMax,
        ICollection<string> diagnostics)
    {
        if (playerRow.Count == 0)
            return;
        var assigned = new HashSet<int>();
        foreach (var player in playerRow.OrderBy(line => line.CenterX))
        {
            if (assigned.Count >= slots.Count)
                break;
            var playerId = SmartBpOcrTextResolver.NormalizeText(player.Text);
            if (string.IsNullOrWhiteSpace(playerId) || IsInvalidPlayerId(playerId))
                continue;
            var slotIndex = ResolveSurvivorSlotIndex(player.CenterX, xMin, xMax, slots.Count);
            if (assigned.Contains(slotIndex))
                slotIndex = Enumerable.Range(0, slots.Count)
                    .Where(index => !assigned.Contains(index))
                    .OrderBy(index => Math.Abs(player.CenterX - slotCenters[index]))
                    .FirstOrDefault();
            slots[slotIndex].PlayerId = playerId;
            assigned.Add(slotIndex);
            diagnostics.Add($"line text=\"{player.Text}\" centerX={player.CenterX:0.0} -> slot={slotIndex}");
        }
    }

    /// <summary>添加行分类诊断日志。</summary>
    private void AddRowClassificationDiagnostics(ScoredRow[] scoredRows, ICollection<string> diagnostics)
    {
        diagnostics.Add("row classification:");
        foreach (var sr in scoredRows)
        {
            var texts = string.Join(", ", sr.Lines.Select(line => line.Text));
            diagnostics.Add($"  row {sr.PhysicalIndex} => {sr.Classification}; reason=coveredSlots={sr.Features.CoveredSlotsCount}, avgConf={sr.Features.AverageConfidence:0.00}, charCount={sr.Features.ValidSurvivorCharacterCount}, unselectedCount={sr.Features.UnselectedCount}, playerIdLike={sr.Features.PlayerIdLikeCount} texts=[{texts}]");
        }
    }

    /// <summary>在 DistributeChara/SurvivorTalent 模式下输出 talent/extra 行忽略诊断。</summary>
    private void AddIgnoredPickedSurRowDiagnostics(
        ScoredRow[] scoredRows,
        ScoredRow characterRow,
        ScoredRow? playerRow,
        ICollection<string> diagnostics)
    {
        var lastSemanticIndex = Math.Max(characterRow.PhysicalIndex, playerRow?.PhysicalIndex ?? characterRow.PhysicalIndex);
        foreach (var sr in scoredRows.Where(sr => sr.PhysicalIndex > lastSemanticIndex && sr.Classification != RowClassification.Noise))
        {
            var texts = string.Join(", ", sr.Lines.Select(line => line.Text));
            diagnostics.Add($"picked_sur ignored talent/extra row {sr.PhysicalIndex} texts=[{texts}]");
            foreach (var line in sr.Lines)
            {
                var resolved = resolver.ResolveCharacterFromLine(line.Text, Camp.Sur, -1, line.Provider);
                if (resolved.ResolvedCharacterKey != null)
                    diagnostics.Add($"picked_sur ignored lower-row character candidate row={sr.PhysicalIndex} raw={line.Text} result={resolved.ResolvedCharacterKey} reason=below-player-id-row");
            }
        }
    }

    /// <summary>行结构特征。</summary>
    private sealed class RowFeatures
    {
        public int ItemCount { get; set; }
        public int CoveredSlotsCount { get; set; }
        public double AverageConfidence { get; set; }
        public int ValidSurvivorCharacterCount { get; set; }
        public int UnselectedCount { get; set; }
        public int PlayerIdLikeCount { get; set; }
        public int ShortFragmentCount { get; set; }
        public bool HasFourSlotStructure { get; set; }
        public bool HasMostlySlotLikeTexts { get; set; }
        public bool IsSingleLowValueFragmentRow { get; set; }
    }

    /// <summary>行分类标签。</summary>
    private enum RowClassification
    {
        Unknown,
        Noise,
        Character,
        PlayerId,
        Talent
    }

    /// <summary>带评分和分类的行。</summary>
    private sealed record ScoredRow(
        int PhysicalIndex,
        IReadOnlyList<OcrLineLayout> Lines,
        RowFeatures Features,
        RowClassification Classification);

    private void ApplyPickedSurCharacterSlot(
        SmartBpRecognizedPlayerCharacterSlot slot,
        OcrLineLayout item,
        ICollection<string> diagnostics)
    {
        var resolved = resolver.ResolveCharacterFromLine(item.Text, Camp.Sur, slot.Index, item.Provider);
        AddResolverDiagnostics(diagnostics, "left_bottom", item.Line, resolved);
        if (resolved.ResolvedCharacterKey != null)
        {
            slot.CharacterName = resolved.ResolvedCharacterKey;
            ApplyRecognitionMetadata(slot, resolved);
        }
        else if (SmartBpBusinessStateParser.IsUnselected(item.Text))
        {
            slot.CharacterName = "未选择";
        }
    }

    private void AssignPickedSurPlayerIds(
        IReadOnlyList<SmartBpRecognizedPlayerCharacterSlot> slots,
        IReadOnlyList<double> slotCenters,
        IReadOnlyList<OcrLineLayout> playerRow)
    {
        if (playerRow.Count == 0)
            return;

        var assigned = new HashSet<int>();
        foreach (var player in playerRow.OrderBy(line => line.CenterX))
        {
            if (assigned.Count >= slots.Count)
                break;

            var playerId = SmartBpOcrTextResolver.NormalizeText(player.Text);
            if (string.IsNullOrWhiteSpace(playerId) || IsInvalidPlayerId(playerId))
                continue;

            var slotIndex = Enumerable.Range(0, slots.Count)
                .Where(index => !assigned.Contains(index))
                .OrderBy(index => Math.Abs(player.CenterX - slotCenters[index]))
                .FirstOrDefault();
            slots[slotIndex].PlayerId = playerId;
            assigned.Add(slotIndex);
        }
    }

    private static double[] BuildPickedSurSlotCenters(
        IReadOnlyList<OcrLineLayout> characterRow,
        IReadOnlyList<OcrLineLayout> playerRow,
        int count)
    {
        var centers = new double[count];
        if (characterRow.Count >= count)
        {
            for (var i = 0; i < count; i++)
                centers[i] = characterRow[i].CenterX;
            return centers;
        }

        var ordered = characterRow.Concat(playerRow)
            .OrderBy(line => line.CenterX)
            .Select(line => line.CenterX)
            .Distinct()
            .Take(count)
            .ToArray();
        for (var i = 0; i < count; i++)
            centers[i] = i < ordered.Length ? ordered[i] : (ordered.DefaultIfEmpty(0).Last() + (i - ordered.Length + 1) * 100);
        return centers;
    }

    private void AddIgnoredPickedSurRowDiagnostics(
        IReadOnlyList<IReadOnlyList<OcrLineLayout>> rows,
        ICollection<string> diagnostics)
    {
        for (var rowIndex = 2; rowIndex < rows.Count; rowIndex++)
        {
            var texts = rows[rowIndex].Select(line => line.Text).ToArray();
            diagnostics.Add($"picked_sur ignored talent/extra row {rowIndex} texts=[{string.Join(", ", texts)}]");
            foreach (var line in rows[rowIndex])
            {
                var resolved = resolver.ResolveCharacterFromLine(line.Text, Camp.Sur, -1, line.Provider);
                if (resolved.ResolvedCharacterKey != null)
                    diagnostics.Add($"picked_sur ignored lower-row character candidate row={rowIndex} raw={line.Text} result={resolved.ResolvedCharacterKey} reason=below-player-id-row");
            }
        }
    }

    private static void AddPickedSurRowDiagnostics(
        IReadOnlyList<IReadOnlyList<OcrLineLayout>> rows,
        ICollection<string> diagnostics)
    {
        diagnostics.Add("picked_sur row clustering:");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            var minY = row.Min(line => line.CenterY);
            var maxY = row.Max(line => line.CenterY);
            var meanY = row.Average(line => line.CenterY);
            diagnostics.Add($"row {rowIndex} y={meanY:0.#} range={minY:0.#}-{maxY:0.#} texts=[{string.Join(", ", row.Select(line => line.Text))}]");
        }
    }

    private static OcrLineLayout CreateLayoutLine(SmartBpRecognitionRegion region, OcrTextLine line)
    {
        var centerX = line.BoundingBox.Width > 0 ? line.BoundingBox.X + line.BoundingBox.Width / 2d : line.CenterX;
        var centerY = line.BoundingBox.Height > 0 ? line.BoundingBox.Y + line.BoundingBox.Height / 2d : line.CenterY;
        return new(region, SmartBpOcrTextResolver.NormalizeText(line.Text), line.Confidence, line.BoundingBox, centerX, centerY, line.Provider, line);
    }

    private static double CalculateRowTolerance(IReadOnlyList<OcrLineLayout> lines)
    {
        var heights = lines
            .Select(line => line.BoundingBox.Height)
            .Where(height => height > 0)
            .OrderBy(height => height)
            .ToArray();
        if (heights.Length == 0)
            return 12;
        var median = (double)heights[heights.Length / 2];
        if (heights.Length % 2 == 0)
            median = (heights[heights.Length / 2 - 1] + median) / 2d;
        return Math.Max(12, median * .75);
    }

    private static IReadOnlyList<IReadOnlyList<OcrLineLayout>> ClusterRows(
        IEnumerable<OcrLineLayout> lines,
        double rowTolerance)
    {
        var rows = new List<List<OcrLineLayout>>();
        foreach (var line in lines.OrderBy(line => line.CenterY).ThenBy(line => line.CenterX))
        {
            var current = rows.LastOrDefault();
            if (current == null || Math.Abs(line.CenterY - current.Average(item => item.CenterY)) > rowTolerance)
                rows.Add([line]);
            else
                current.Add(line);
        }

        return rows
            .Select(row => (IReadOnlyList<OcrLineLayout>)row.OrderBy(line => line.CenterX).ToArray())
            .ToArray();
    }

    private sealed record OcrLineLayout(
        SmartBpRecognitionRegion Region,
        string Text,
        double Confidence,
        Rect BoundingBox,
        double CenterX,
        double CenterY,
        string? Provider,
        OcrTextLine Line);

    private SmartBpFocusedBusinessExtractionResult ParseHunterPickRegion(
        IReadOnlyList<OcrTextLine> lines,
        ICollection<string> diagnostics)
    {
        var contentLines = lines.Where(line => !IsStatusLine(line.Text)).ToArray();
        var candidates = contentLines
            .Select(line => new { Line = line, Character = resolver.ResolveCharacterFromLine(line.Text, Camp.Hun, 0, line.Provider) })
            .ToArray();
        foreach (var candidate in candidates)
            AddResolverDiagnostics(diagnostics, "right_bottom", candidate.Line, candidate.Character);
        var anchor = candidates
            .Where(item => item.Character.ResolvedCharacterKey != null)
            .OrderBy(item => item.Line.CenterY)
            .ThenBy(item => item.Line.CenterX)
            .FirstOrDefault();
        var slot = new SmartBpRecognizedPlayerCharacterSlot { Index = 0, CharacterName = "未选择" };
        if (anchor != null)
        {
            slot.CharacterName = anchor.Character.ResolvedCharacterKey!;
            ApplyRecognitionMetadata(slot, anchor.Character);
            slot.PlayerId = FindNearestPlayerIdBelow(anchor.Line, 0, [anchor.Line], contentLines, Camp.Hun);
        }

        diagnostics.Add($"picked_hun: parsed {slot.CharacterName}/{slot.PlayerId ?? "null"}");
        return new() { Phase = "未知", TargetField = "picked_hun", PickedHun = slot };
    }

    private string? FindNearestPlayerIdBelow(
        OcrTextLine anchor,
        int anchorIndex,
        IReadOnlyList<OcrTextLine> anchors,
        IReadOnlyList<OcrTextLine> contentLines,
        Camp camp)
    {
        var leftBoundary = anchorIndex == 0
            ? double.NegativeInfinity
            : (anchors[anchorIndex - 1].CenterX + anchor.CenterX) / 2;
        var rightBoundary = anchorIndex >= anchors.Count - 1
            ? double.PositiveInfinity
            : (anchor.CenterX + anchors[anchorIndex + 1].CenterX) / 2;
        return contentLines
            .Where(line => !ReferenceEquals(line, anchor))
            .Where(line => line.CenterY > anchor.CenterY)
            .Where(line => line.CenterX >= leftBoundary && line.CenterX <= rightBoundary)
            .Where(line => resolver.ResolveCharacterFromLine(line.Text, camp, anchorIndex, line.Provider).ResolvedCharacterKey == null)
            .Where(line => !IsStatusLine(line.Text))
            .OrderBy(line => line.CenterY)
            .ThenBy(line => Math.Abs(line.CenterX - anchor.CenterX))
            .Select(line => SmartBpOcrTextResolver.NormalizeText(line.Text))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text) && !IsInvalidPlayerId(text));
    }

    private static void AddResolverDiagnostics(
        ICollection<string> diagnostics,
        string region,
        OcrTextLine line,
        SmartBpNormalizedCharacter character)
    {
        var result = character.ResolvedCharacterName ?? "unresolved";
        diagnostics.Add($"ocr-match region={region} raw={character.RawCharacterName ?? line.Text} provider={line.Provider ?? "unknown"} ocrConf={line.Confidence:0.00} camp={character.Camp} result={result} matchMode={character.MatchMode} score={character.Confidence:0.00} safe={character.IsAutoApplySafe} reason={character.RecognitionReason ?? string.Join(" | ", character.Warnings)}");
    }

    private static List<SmartBpRecognizedPlayerCharacterSlot> DefaultPlayerSlots(int count) =>
        Enumerable.Range(0, count)
            .Select(index => new SmartBpRecognizedPlayerCharacterSlot { Index = index, CharacterName = "未选择" })
            .ToList();

    private static void ApplyRecognitionMetadata(
        SmartBpRecognizedCharacterSlot slot,
        SmartBpNormalizedCharacter character)
    {
        slot.RecognitionConfidence = character.Confidence;
        slot.IsAutoApplySafe = character.IsAutoApplySafe && character.Confidence >= .90;
        slot.RecognitionReason = $"matchMode={character.MatchMode}; {character.RecognitionReason ?? character.Warnings.FirstOrDefault()}";
    }

    private static bool IsStatusLine(string text)
    {
        var normalized = SmartBpOcrTextResolver.NormalizeForMatch(text);
        string[] markers =
        [
            "等待中", "屏蔽求生者", "屏蔽监管者", "禁用求生者", "禁用监管者",
            "选择求生者", "选择监管者", "求生者选择角色中", "选择天赋中", "天赋已锁定"
        ];
        return markers.Any(marker => normalized.Contains(SmartBpOcrTextResolver.NormalizeForMatch(marker), StringComparison.Ordinal));
    }

    private static bool IsInvalidPlayerId(string text)
    {
        var normalized = SmartBpOcrTextResolver.NormalizeForMatch(text);
        string[] statusValues =
        [
            "已选择", "未选择", "等待选择", "等待中", "选择中", "天赋已锁定",
            "区域选择", "等待游戏开始", "前往", "剩余"
        ];
        return statusValues.Any(status => normalized.Equals(
            SmartBpOcrTextResolver.NormalizeForMatch(status), StringComparison.Ordinal));
    }
}

/// <summary>
/// 从顶部生命周期状态区域识别 BP 进行中、天赋调整或区域选择等生命周期阶段。
/// </summary>
internal sealed class SmartBpLifecycleStatusDetector : ISmartBpLifecycleStatusDetector
{
    private const double WeakThreshold = .65;

    /// <inheritdoc />
    public SmartBpLifecycleStatusResult Detect(IReadOnlyList<OcrTextLine> lines)
    {
        var rawLines = lines.Select(line => line.Text).Where(text => !string.IsNullOrWhiteSpace(text)).ToArray();
        var evidence = string.Join(" / ", rawLines);
        var normalized = string.Concat(rawLines.Select(SmartBpOcrTextResolver.NormalizeForMatch));
        var hasDestination = BestSubstringSimilarity(normalized, "前往") >= .5;
        var matches = Candidates.Select(candidate =>
        {
            var phraseScore = BestSubstringSimilarity(normalized, candidate.Status);
            var keywordHits = candidate.Tokens.Count(token =>
                BestSubstringSimilarity(normalized, token) >= (token.Length <= 2 ? .5 : .67));
            var coverage = (double)keywordHits / candidate.Tokens.Length;
            var score = phraseScore * .72 + coverage * .28 + (hasDestination ? .03 : 0);
            return new { Candidate = candidate, PhraseScore = phraseScore, Coverage = coverage, Score = Math.Min(1, score) };
        }).OrderByDescending(match => match.Score).First();

        var recognized = matches.Score >= WeakThreshold && matches.Coverage >= .5;
        var status = recognized ? matches.Candidate.Status : "未知";
        var category = recognized ? matches.Candidate.Category : SmartBpLifecycleCategory.Unknown;
        var reason = $"phrase similarity={matches.PhraseScore:0.00} + keyword coverage={matches.Coverage:0.00}";
        var diagnostics = new[]
        {
            $"TopCenterStatus raw=\"{evidence}\"",
            $"normalized=\"{normalized}\"",
            $"best_match=\"{matches.Candidate.Status}\" score={matches.Score:0.00} category={category} reason=\"{reason}\""
        };
        return new SmartBpLifecycleStatusResult
        {
            IsRecognized = recognized,
            Status = status,
            Category = category,
            Score = matches.Score,
            Evidence = evidence,
            NormalizedText = normalized,
            HasDestinationEvidence = hasDestination,
            Diagnostics = diagnostics
        };
    }

    private static readonly (string Status, SmartBpLifecycleCategory Category, string[] Tokens)[] Candidates =
    [
        ("阵营选择中", SmartBpLifecycleCategory.CharacterBpActive, ["阵营", "选择"]),
        ("求生者天赋特质调整", SmartBpLifecycleCategory.SurvivorTalentAdjust, ["求生者", "天赋", "特质", "调整"]),
        ("监管者天赋特质调整", SmartBpLifecycleCategory.HunterTalentAdjust, ["监管者", "天赋", "特质", "调整"]),
        ("即将进入区域选择", SmartBpLifecycleCategory.TransitionToAreaSelection, ["即将", "进入", "区域", "选择"])
    ];

    private static double BestSubstringSimilarity(string text, string candidate)
    {
        text = SmartBpOcrTextResolver.NormalizeForMatch(text);
        candidate = SmartBpOcrTextResolver.NormalizeForMatch(candidate);
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(candidate)) return 0;
        if (text.Contains(candidate, StringComparison.Ordinal)) return 1;
        var best = 0d;
        var minLength = Math.Max(1, candidate.Length - 2);
        var maxLength = Math.Min(text.Length, candidate.Length + 2);
        for (var length = minLength; length <= maxLength; length++)
        for (var start = 0; start + length <= text.Length; start++)
        {
            var distance = EditDistance(text.AsSpan(start, length), candidate.AsSpan());
            best = Math.Max(best, 1d - (double)distance / Math.Max(length, candidate.Length));
        }
        return best;
    }

    private static int EditDistance(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }
}

/// <summary>
/// 使用纯 OCR 路径识别当前 BP 阶段和各角色区域业务状态。
/// </summary>
internal sealed class SmartBpOcrBpRecognitionService(
    IOcrService ocr,
    ISmartBpRecognitionFrameCropper cropper,
    ISmartBpOcrContactSheetBuilder contactSheetBuilder,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpBusinessStateMerger merger,
    SmartBpOcrRegionParser parser,
    ISmartBpLifecycleStatusDetector lifecycleDetector) : ISmartBpOcrBpRecognitionService
{
    public async Task<SmartBpOcrRecognitionResult> RecognizeAsync(
        BitmapSource frame,
        SmartBpOcrRecognitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var diagnostics = new List<string>();
        diagnostics.Add($"OCR provider selected: {ocr.SelectedProvider}; fallback=false.");
        var requestedRegions = BuildRequestedRegions(request);
        var regionTexts = settings.Settings.UseOcrContactSheet
            ? await RecognizeContactSheetAsync(frame, requestedRegions, diagnostics, cancellationToken).ConfigureAwait(false)
            : await RecognizePerRegionAsync(frame, requestedRegions, diagnostics, cancellationToken).ConfigureAwait(false);

        var dimensions = await GetRegionDimensionsAsync(frame, requestedRegions, cancellationToken).ConfigureAwait(false);
        var phaseLines = regionTexts.FirstOrDefault(item => item.Region == SmartBpRecognitionRegion.PhaseTop)?.Lines ?? [];
        var lifecycleLines = regionTexts.FirstOrDefault(item => item.Region == SmartBpRecognitionRegion.TopCenterStatus)?.Lines ?? [];
        var statusLines = regionTexts.FirstOrDefault(item => item.Region == SmartBpRecognitionRegion.TopLeftStatus)?.Lines ?? [];
        var phaseWidth = (double)dimensions.GetValueOrDefault(SmartBpRecognitionRegion.PhaseTop).Width;
        if (phaseWidth <= 0)
            phaseWidth = phaseLines.Select(line => line.CenterX).DefaultIfEmpty(1).Max() * 2;
        var postBpStatus = SmartBpPostBpStatusDetector.Detect(statusLines);
        SmartBpLifecycleStatusResult? lifecycleStatus = null;
        if (requestedRegions.Contains(SmartBpRecognitionRegion.TopCenterStatus))
        {
            lifecycleStatus = lifecycleDetector.Detect(lifecycleLines);
            foreach (var diagnostic in lifecycleStatus.Diagnostics)
                diagnostics.Add(diagnostic);
        }
        diagnostics.Add($"TopLeftStatus OCR raw text={postBpStatus.Evidence}");
        diagnostics.Add($"TopLeftStatus OCR normalized={postBpStatus.NormalizedText}");
        diagnostics.Add($"TopLeftStatus OCR matched_title={(postBpStatus.IsPostBp ? postBpStatus.Phase : "none")}; score={postBpStatus.Score:0.00}; auxiliary=[{string.Join(", ", postBpStatus.AuxiliaryEvidence)}]");
        SmartBpPhaseRecognitionResult phase;
        if (postBpStatus.IsPostBp)
        {
            phase = new SmartBpPhaseRecognitionResult { Phase = postBpStatus.Phase };
            diagnostics.Add($"Pure OCR post-BP fuzzy anchor matched: phase={postBpStatus.Phase}; evidence=\"{postBpStatus.Evidence}\"; score={postBpStatus.Score:0.00}; reason={postBpStatus.Reason}.");
        }
        else
        {
            phase = SmartBpOcrPhaseClassifier.Classify(phaseLines, phaseWidth, diagnostics);
        }

        var parsed = new Dictionary<SmartBpRecognitionRegion, SmartBpFocusedBusinessExtractionResult>();
        var effectiveParseContext = request.ParseContext ?? new SmartBpOcrFieldParseContext { AuthoritativePhase = phase.Phase };
        foreach (var regionText in regionTexts.Where(item => item.Region is not SmartBpRecognitionRegion.PhaseTop and not SmartBpRecognitionRegion.TopCenterStatus and not SmartBpRecognitionRegion.TopLeftStatus))
        {
            var parsedRegion = parser.ParseDetailed(regionText.Region, regionText.Lines, effectiveParseContext);
            parsed[regionText.Region] = parsedRegion.Result;
            diagnostics.AddRange(parsedRegion.Diagnostics);
            foreach (var line in regionText.Lines)
                diagnostics.Add($"provider={line.Provider ?? "unknown"}; region={ToRegionId(regionText.Region)}; coordinateSpace=region-local; text={line.Text}; bbox={line.BoundingBox}; center={line.CenterX:0.0},{line.CenterY:0.0}; confidence={line.Confidence:0.00}");
        }

        var state = merger.Merge(
            phase,
            parsed.GetValueOrDefault(SmartBpRecognitionRegion.RightTop),
            parsed.GetValueOrDefault(SmartBpRecognitionRegion.LeftTop),
            parsed.GetValueOrDefault(SmartBpRecognitionRegion.LeftBottom),
            parsed.GetValueOrDefault(SmartBpRecognitionRegion.RightBottom));
        return new()
        {
            Phase = phase,
            BusinessState = state,
            Regions = regionTexts,
            LifecycleStatus = lifecycleStatus,
            PostBpStatus = requestedRegions.Contains(SmartBpRecognitionRegion.TopLeftStatus) ? postBpStatus : null,
            Diagnostics = diagnostics
        };
    }

    private async Task<IReadOnlyList<SmartBpOcrRegionText>> RecognizeContactSheetAsync(
        BitmapSource frame,
        IReadOnlyList<SmartBpRecognitionRegion> requestedRegions,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var sheet = contactSheetBuilder.Build(frame, requestedRegions);
            var result = ocr.RecognizeTextLines(sheet.Image);
            var grouped = SmartBpOcrContactSheetMapper.MapLinesToRegions(result, sheet.Regions, out var unmapped);
            diagnostics.Add($"provider={result.Provider ?? ocr.SelectedProvider.ToString()}; line_count={result.Lines.Count}; OCR contact sheet regions=[{string.Join(", ", requestedRegions.Select(ToRegionId))}], unmapped={unmapped}.");
            foreach (var region in sheet.Regions.Where(region => region.Region is SmartBpRecognitionRegion.TopCenterStatus or SmartBpRecognitionRegion.TopLeftStatus))
                AddStatusCropDiagnostics(diagnostics, cropper.CropWithInfo(frame, region.Region));
            return grouped;
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SmartBpOcrRegionText>> RecognizePerRegionAsync(
        BitmapSource frame,
        IReadOnlyList<SmartBpRecognitionRegion> requestedRegions,
        ICollection<string> diagnostics,
        CancellationToken cancellationToken)
    {
        var groups = new List<SmartBpOcrRegionText>();
        foreach (var region in requestedRegions)
        {
            var lines = await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var crop = cropper.CropWithInfo(frame, region);
                using var raw = BitmapSourceConverter.ToMat(crop.Image);
                using var bgr = ToBgr(raw);
                var result = ocr.RecognizeTextLines(bgr).Lines;
                if (region is SmartBpRecognitionRegion.TopCenterStatus or SmartBpRecognitionRegion.TopLeftStatus)
                    AddStatusCropDiagnostics(diagnostics, crop);
                return result;
            }, cancellationToken).ConfigureAwait(false);
            diagnostics.Add($"provider={ocr.SelectedProvider}; line_count={lines.Count}; OCR per-region fallback={ToRegionId(region)}.");
            groups.Add(new() { Region = region, Lines = lines });
        }

        return groups;
    }

    private async Task<IReadOnlyDictionary<SmartBpRecognitionRegion, Size>> GetRegionDimensionsAsync(
        BitmapSource frame,
        IReadOnlyList<SmartBpRecognitionRegion> requestedRegions,
        CancellationToken cancellationToken) =>
        await Task.Run(() => requestedRegions
            .Distinct()
            .Select(region => cropper.CropWithInfo(frame, region))
            .ToDictionary(crop => crop.Region, crop => new Size(crop.Width, crop.Height)), cancellationToken).ConfigureAwait(false);

    private static IReadOnlyList<SmartBpRecognitionRegion> BuildRequestedRegions(SmartBpOcrRecognitionRequest request)
    {
        var regions = new List<SmartBpRecognitionRegion>();
        if (request.IncludePhase)
        {
            regions.Add(SmartBpRecognitionRegion.PhaseTop);
            regions.Add(SmartBpRecognitionRegion.TopLeftStatus);
        }
        regions.AddRange(request.ContentRegions.Where(region => region != SmartBpRecognitionRegion.PhaseTop));
        return regions.Distinct().ToArray();
    }

    private static Mat ToBgr(Mat source)
    {
        var result = new Mat();
        if (source.Channels() == 1)
            Cv2.CvtColor(source, result, ColorConversionCodes.GRAY2BGR);
        else if (source.Channels() == 4)
            Cv2.CvtColor(source, result, ColorConversionCodes.BGRA2BGR);
        else
            source.CopyTo(result);
        return result;
    }

    internal static string ToRegionId(SmartBpRecognitionRegion region) => region switch
    {
        SmartBpRecognitionRegion.PhaseTop => "phase_top",
        SmartBpRecognitionRegion.TopCenterStatus => "top_center_status",
        SmartBpRecognitionRegion.TopLeftStatus => "top_left_status",
        SmartBpRecognitionRegion.LeftTop => "left_top",
        SmartBpRecognitionRegion.RightTop => "right_top",
        SmartBpRecognitionRegion.LeftBottom => "left_bottom",
        SmartBpRecognitionRegion.RightBottom => "right_bottom",
        _ => region.ToString()
    };

    private static void AddStatusCropDiagnostics(ICollection<string> diagnostics, SmartBpCroppedFrame crop)
    {
        var id = ToRegionId(crop.Region);
        diagnostics.Add($"{id} crop source={crop.LayoutSource}");
        diagnostics.Add($"{id} normalized rect={crop.NormalizedRectText}");
        diagnostics.Add($"{id} pixel rect={crop.PixelRectText}");
    }
}

/// <summary>
/// 将纯 OCR 业务状态识别结果转换为自动识别状态机使用的快照增量。
/// </summary>
internal sealed class SmartBpOcrSnapshotDeltaRecognitionService(
    ISmartBpOcrBpRecognitionService ocrRecognition,
    ISmartBpRecognitionFrameCropper cropper) : ISmartBpOcrSnapshotDeltaRecognitionService
{
    public async Task<(SmartBpSnapshotDeltaResult Delta, IReadOnlyDictionary<string, string> RawResponses, SmartBpCroppedFrame PhaseCrop, IReadOnlyList<SmartBpCroppedFrame> ContentCrops, IReadOnlyList<string> Diagnostics)> RecognizeDeltaAsync(
        BitmapSource frame,
        SmartBpSnapshotDeltaRequest request,
        long frameSequence,
        CancellationToken cancellationToken = default)
    {
        var watch = Stopwatch.StartNew();
        var contentRegions = request.RequestedRegions.Select(item => item.Region).Distinct().ToArray();
        var result = await ocrRecognition.RecognizeAsync(frame, new SmartBpOcrRecognitionRequest(contentRegions), cancellationToken).ConfigureAwait(false);
        watch.Stop();

        var delta = ToDelta(result.BusinessState, request.RequestedFields);
        var phaseCrop = await Task.Run(() => cropper.CropWithInfo(frame, SmartBpRecognitionRegion.PhaseTop), cancellationToken).ConfigureAwait(false);
        var crops = new List<SmartBpCroppedFrame>();
        foreach (var region in contentRegions)
            crops.Add(await Task.Run(() => cropper.CropWithInfo(frame, region), cancellationToken).ConfigureAwait(false));

        var diagnostics = new List<string>
        {
            $"Frame sequence {frameSequence}: OCR requested fields [{string.Join(", ", request.RequestedFields)}].",
            $"OCR elapsed time {watch.ElapsedMilliseconds}ms.",
            $"OCR delta updates=[{string.Join(", ", delta.Updates.Select(update => update.Field))}]."
        };
        diagnostics.AddRange(request.Diagnostics);
        diagnostics.AddRange(result.Diagnostics);
        return (delta, new Dictionary<string, string> { ["ocr_lines"] = FormatRawLines(result) }, phaseCrop, crops, diagnostics);
    }

    private static SmartBpSnapshotDeltaResult ToDelta(
        SmartBpBusinessStateRecognitionResult state,
        IReadOnlyCollection<string> requestedFields)
    {
        var updates = new List<SmartBpSnapshotFieldUpdate>();
        if (requestedFields.Contains("banned_sur"))
            updates.Add(new() { Field = "banned_sur", Slots = state.BannedSur.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("banned_hun"))
            updates.Add(new() { Field = "banned_hun", Slots = state.BannedHun.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("picked_sur"))
            updates.Add(new() { Field = "picked_sur", Slots = state.PickedSur.Select(ToDeltaSlot).ToList() });
        if (requestedFields.Contains("picked_hun"))
            updates.Add(new() { Field = "picked_hun", PickedHun = ToDeltaSlot(state.PickedHun) });
        return new() { Phase = state.Phase, Updates = updates };
    }

    private static string FormatRawLines(SmartBpOcrRecognitionResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"phase={result.Phase.Phase}");
        foreach (var region in result.Regions)
        {
            builder.AppendLine($"[{SmartBpOcrBpRecognitionService.ToRegionId(region.Region)}]");
            foreach (var line in region.Lines)
                builder.AppendLine($"provider={line.Provider ?? "unknown"}\tcoordinateSpace=region-local\ttext={line.Text}\tbbox={line.BoundingBox}\tcenter={line.CenterX:0.0},{line.CenterY:0.0}\tconf={line.Confidence:0.00}");
        }

        return builder.ToString().TrimEnd();
    }

    private static SmartBpSnapshotDeltaSlot ToDeltaSlot(SmartBpRecognizedCharacterSlot slot) =>
        new()
        {
            Index = slot.Index,
            SlotState = SmartBpBusinessStateParser.IsUnselected(slot.CharacterName) ? "unknown" : "selected",
            CharacterName = slot.CharacterName
        };

    private static SmartBpSnapshotDeltaSlot ToDeltaSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new()
        {
            Index = slot.Index,
            SlotState = SmartBpBusinessStateParser.IsUnselected(slot.CharacterName) ? "unknown" : "selected",
            CharacterName = slot.CharacterName,
            PlayerId = slot.PlayerId
        };
}
