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

internal sealed class SmartBpOcrContactSheetBuilder(ISmartBpRecognitionFrameCropper cropper) : ISmartBpOcrContactSheetBuilder
{
    private const int Padding = 24;

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

internal static class SmartBpOcrContactSheetMapper
{
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

internal sealed partial class SmartBpOcrTextResolver(ICharacterSelectionService characterSelectionService) : ISmartBpOcrTextResolver
{
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

    private static bool IsStatusOrPhaseText(string text)
    {
        var normalized = NormalizeForMatch(text);
        string[] markers = ["等待中", "屏蔽求生者", "屏蔽监管者", "禁用求生者", "禁用监管者", "选择求生者", "选择监管者", "求生者选择角色中", "选择天赋中", "天赋已锁定"];
        return markers.Any(marker => normalized.Contains(NormalizeForMatch(marker), StringComparison.Ordinal));
    }

    internal static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Normalize(NormalizationForm.FormKC).Trim();

    internal static string NormalizeForMatch(string? value)
    {
        var normalized = NormalizeText(value);
        normalized = StripDecorativeQuotes(normalized);
        return NonWordRegex().Replace(normalized, "").ToUpperInvariant();
    }

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

internal static class SmartBpOcrPhaseClassifier
{
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

    private static SmartBpPhaseRecognitionResult Matched(string phase, string message, ICollection<string> diagnostics)
    {
        diagnostics.Add(message);
        diagnostics.Add($"final phase: {phase}");
        return new() { Phase = phase };
    }

    private static bool IsLeft(OcrTextLine line, double width) => line.CenterX < width * .5;

    private static bool ContainsBanSur(string text) =>
        ContainsNormalized(text, "屏蔽求生者") || ContainsNormalized(text, "禁用求生者");

    private static bool ContainsBanHun(string text) =>
        ContainsNormalized(text, "屏蔽监管者") || ContainsNormalized(text, "禁用监管者");

    private static bool ContainsNormalized(string text, string candidate) =>
        SmartBpOcrTextResolver.NormalizeForMatch(text).Contains(SmartBpOcrTextResolver.NormalizeForMatch(candidate), StringComparison.Ordinal);
}

internal static class SmartBpPostBpStatusDetector
{
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

internal sealed class SmartBpOcrRegionParser(ISmartBpOcrTextResolver resolver)
{
    public SmartBpOcrParsedRegionResult ParseDetailed(
        SmartBpRecognitionRegion region,
        IReadOnlyList<OcrTextLine> lines)
    {
        var diagnostics = new List<string>();
        foreach (var line in lines.Where(line => IsStatusLine(line.Text)))
            diagnostics.Add($"ocr-ignore region={SmartBpOcrBpRecognitionService.ToRegionId(region)} raw={line.Text} provider={line.Provider ?? "unknown"} confidence={line.Confidence:0.00} reason=status-line");
        var result = Parse(region, lines, diagnostics);
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
        ICollection<string> diagnostics)
    {
        return region switch
        {
            SmartBpRecognitionRegion.RightTop => ParseBanRegion(lines, Camp.Sur, "banned_sur", 4, diagnostics),
            SmartBpRecognitionRegion.LeftTop => ParseBanRegion(lines, Camp.Hun, "banned_hun", 2, diagnostics),
            SmartBpRecognitionRegion.LeftBottom => ParseSurvivorPickRegion(lines, diagnostics),
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
        ICollection<string> diagnostics)
    {
        var slots = DefaultPlayerSlots(4);
        var layoutLines = lines
            .Where(line => !IsStatusLine(line.Text))
            .Select(line => CreateLayoutLine(SmartBpRecognitionRegion.LeftBottom, line))
            .Where(line => !string.IsNullOrWhiteSpace(line.Text))
            .ToArray();
        var rows = ClusterRows(layoutLines, CalculateRowTolerance(layoutLines));
        AddPickedSurRowDiagnostics(rows, diagnostics);

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
        diagnostics.Add("picked_sur slot assignment:");
        foreach (var slot in slots)
            diagnostics.Add($"slot {slot.Index} char={slot.CharacterName} player_id={slot.PlayerId ?? "null"}");
        diagnostics.Add($"picked_sur: parsed [{string.Join(", ", slots.Select(slot => $"{slot.Index}={slot.CharacterName}/{slot.PlayerId ?? "null"}"))}]");
        return new() { Phase = "未知", TargetField = "picked_sur", Slots = slots };
    }

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

internal sealed class SmartBpOcrBpRecognitionService(
    IOcrService ocr,
    ISmartBpRecognitionFrameCropper cropper,
    ISmartBpOcrContactSheetBuilder contactSheetBuilder,
    ISmartBpRecognitionSettingsService settings,
    ISmartBpBusinessStateMerger merger,
    SmartBpOcrRegionParser parser) : ISmartBpOcrBpRecognitionService
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
        var statusLines = regionTexts.FirstOrDefault(item => item.Region == SmartBpRecognitionRegion.TopLeftStatus)?.Lines ?? [];
        var phaseWidth = (double)dimensions.GetValueOrDefault(SmartBpRecognitionRegion.PhaseTop).Width;
        if (phaseWidth <= 0)
            phaseWidth = phaseLines.Select(line => line.CenterX).DefaultIfEmpty(1).Max() * 2;
        var postBpStatus = SmartBpPostBpStatusDetector.Detect(statusLines);
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
        foreach (var regionText in regionTexts.Where(item => item.Region is not SmartBpRecognitionRegion.PhaseTop and not SmartBpRecognitionRegion.TopLeftStatus))
        {
            var parsedRegion = parser.ParseDetailed(regionText.Region, regionText.Lines);
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
            foreach (var region in sheet.Regions.Where(region => region.Region == SmartBpRecognitionRegion.TopLeftStatus))
                diagnostics.Add($"top_left_status crop=x={region.OriginalFrameRect.X}, y={region.OriginalFrameRect.Y}, width={region.OriginalFrameRect.Width}, height={region.OriginalFrameRect.Height}");
            var statusCrop = cropper.CropWithInfo(frame, SmartBpRecognitionRegion.TopLeftStatus);
            AddTopLeftStatusCropDiagnostics(diagnostics, statusCrop);
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
                if (region == SmartBpRecognitionRegion.TopLeftStatus)
                    AddTopLeftStatusCropDiagnostics(diagnostics, crop);
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
        SmartBpRecognitionRegion.TopLeftStatus => "top_left_status",
        SmartBpRecognitionRegion.LeftTop => "left_top",
        SmartBpRecognitionRegion.RightTop => "right_top",
        SmartBpRecognitionRegion.LeftBottom => "left_bottom",
        SmartBpRecognitionRegion.RightBottom => "right_bottom",
        _ => region.ToString()
    };

    private static void AddTopLeftStatusCropDiagnostics(ICollection<string> diagnostics, SmartBpCroppedFrame crop)
    {
        diagnostics.Add($"top_left_status crop source={crop.LayoutSource}");
        diagnostics.Add($"top_left_status normalized rect={crop.NormalizedRectText}");
        diagnostics.Add($"top_left_status pixel rect={crop.PixelRectText}");
    }
}

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

internal sealed class SmartBpSnapshotDeltaRecognitionRouter(
    ISmartBpRecognitionSettingsService settings,
    SmartBpAiSnapshotDeltaRecognitionService ai,
    ISmartBpOcrSnapshotDeltaRecognitionService ocr) : ISmartBpSnapshotDeltaRecognitionService
{
    public Task<(SmartBpSnapshotDeltaResult Delta, IReadOnlyDictionary<string, string> RawResponses, SmartBpCroppedFrame PhaseCrop, IReadOnlyList<SmartBpCroppedFrame> ContentCrops, IReadOnlyList<string> Diagnostics)> RecognizeDeltaAsync(
        BitmapSource frame,
        SmartBpSnapshotDeltaRequest request,
        long frameSequence,
        CancellationToken cancellationToken = default)
    {
        return settings.Settings.RecognitionEngine == SmartBpRecognitionEngine.Ocr && settings.Settings.EnableOcrBpRecognition
            ? ocr.RecognizeDeltaAsync(frame, request, frameSequence, cancellationToken)
            : ai.RecognizeDeltaAsync(frame, request, frameSequence, cancellationToken);
    }
}
