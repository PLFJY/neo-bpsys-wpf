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

internal sealed class SmartBpOcrRegionParser(ISmartBpOcrTextResolver resolver)
{
    public SmartBpOcrParsedRegionResult ParseDetailed(
        SmartBpRecognitionRegion region,
        IReadOnlyList<OcrTextLine> lines)
    {
        var diagnostics = new List<string>();
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
        var contentLines = lines.Where(line => !IsStatusLine(line.Text)).ToArray();
        var candidates = contentLines
            .Select(line => new { Line = line, Character = resolver.ResolveCharacterFromLine(line.Text, Camp.Sur, -1, line.Provider) })
            .ToArray();
        foreach (var candidate in candidates)
            AddResolverDiagnostics(diagnostics, "left_bottom", candidate.Line, candidate.Character);
        var anchors = candidates
            .Where(item => item.Character.ResolvedCharacterKey != null)
            .GroupBy(item => item.Character.ResolvedCharacterKey, StringComparer.Ordinal)
            .Select(group => group.OrderBy(item => item.Line.CenterY).First())
            .OrderBy(item => item.Line.CenterX)
            .Take(4)
            .ToArray();
        var slots = DefaultPlayerSlots(4);
        for (var slotIndex = 0; slotIndex < anchors.Length; slotIndex++)
        {
            var anchor = anchors[slotIndex];
            slots[slotIndex].CharacterName = anchor.Character.ResolvedCharacterKey!;
            ApplyRecognitionMetadata(slots[slotIndex], anchor.Character);
            slots[slotIndex].PlayerId = FindNearestPlayerIdBelow(anchor.Line, slotIndex, anchors.Select(item => item.Line).ToArray(), contentLines, Camp.Sur);
        }

        diagnostics.Add($"picked_sur: parsed [{string.Join(", ", slots.Select(slot => $"{slot.Index}={slot.CharacterName}/{slot.PlayerId ?? "null"}"))}]");
        return new() { Phase = "未知", TargetField = "picked_sur", Slots = slots };
    }

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
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
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
        var phaseWidth = (double)dimensions.GetValueOrDefault(SmartBpRecognitionRegion.PhaseTop).Width;
        if (phaseWidth <= 0)
            phaseWidth = phaseLines.Select(line => line.CenterX).DefaultIfEmpty(1).Max() * 2;
        var phase = SmartBpOcrPhaseClassifier.Classify(phaseLines, phaseWidth, diagnostics);

        var parsed = new Dictionary<SmartBpRecognitionRegion, SmartBpFocusedBusinessExtractionResult>();
        foreach (var regionText in regionTexts.Where(item => item.Region != SmartBpRecognitionRegion.PhaseTop))
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
            diagnostics.Add($"OCR contact sheet: regions=[{string.Join(", ", requestedRegions.Select(ToRegionId))}], lines={result.Lines.Count}, unmapped={unmapped}.");
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
                return ocr.RecognizeTextLines(bgr).Lines;
            }, cancellationToken).ConfigureAwait(false);
            diagnostics.Add($"OCR per-region fallback: {ToRegionId(region)}, lines={lines.Count}.");
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
            regions.Add(SmartBpRecognitionRegion.PhaseTop);
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
        SmartBpRecognitionRegion.LeftTop => "left_top",
        SmartBpRecognitionRegion.RightTop => "right_top",
        SmartBpRecognitionRegion.LeftBottom => "left_bottom",
        SmartBpRecognitionRegion.RightBottom => "right_bottom",
        _ => region.ToString()
    };
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
            SlotState = SmartBpBusinessStateParser.IsUnselected(slot.CharacterName) ? "empty" : "selected",
            CharacterName = slot.CharacterName
        };

    private static SmartBpSnapshotDeltaSlot ToDeltaSlot(SmartBpRecognizedPlayerCharacterSlot slot) =>
        new()
        {
            Index = slot.Index,
            SlotState = SmartBpBusinessStateParser.IsUnselected(slot.CharacterName) ? "empty" : "selected",
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
