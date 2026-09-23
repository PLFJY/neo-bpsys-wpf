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

internal sealed class SmartBpOcrRegionParser(ISmartBpOcrTextResolver resolver)
{
    public SmartBpOcrParsedRegionResult ParseDetailed(
        SmartBpRecognitionRegion region,
        IReadOnlyList<OcrTextLine> lines,
        SmartBpOcrFieldParseContext? parseContext = null,
        double regionWidth = 0)
    {
        var diagnostics = new List<string>();
        foreach (var line in lines.Where(line => IsStatusLine(line.Text)))
            diagnostics.Add($"ocr-ignore region={SmartBpOcrBpRecognitionService.ToRegionId(region)} raw={line.Text} provider={line.Provider ?? "unknown"} confidence={line.Confidence:0.00} reason=status-line");
        var result = Parse(region, lines, diagnostics, parseContext, regionWidth);
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
        SmartBpOcrFieldParseContext? parseContext = null,
        double regionWidth = 0)
    {
        return region switch
        {
            SmartBpRecognitionRegion.RightTop => ParseBanRegion(lines, Camp.Sur, "banned_sur", 4, diagnostics, regionWidth),
            SmartBpRecognitionRegion.LeftTop => ParseBanRegion(lines, Camp.Hun, "banned_hun", 2, diagnostics, regionWidth),
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
        ICollection<string> diagnostics,
        double regionWidth)
    {
        var regionId = field == "banned_sur" ? "right_top" : "left_top";
        var contentLines = lines.Where(line => !IsStatusLine(line.Text)).ToArray();
        if (regionWidth <= 0)
            regionWidth = contentLines.Select(line => Math.Max(line.BoundingBox.Right, line.CenterX * 2)).DefaultIfEmpty(count).Max();
        var candidates = contentLines
            .Select(line => new
            {
                Line = line,
                Character = resolver.ResolveCharacterFromLine(line.Text, camp, -1, line.Provider),
                IsExplicitEmpty = SmartBpBusinessStateParser.IsUnselected(line.Text)
            })
            .ToArray();
        foreach (var candidate in candidates)
            AddResolverDiagnostics(diagnostics, regionId, candidate.Line, candidate.Character);
        var slots = DefaultPlayerSlots(count);
        foreach (var group in candidates
                     .Where(item => item.Character.ResolvedCharacterName != null || item.IsExplicitEmpty)
                     .GroupBy(item => ResolveBanVisualSlot(item.Line.CenterX, regionWidth, count)))
        {
            var ordered = group
                .OrderByDescending(item => item.Character.IsAutoApplySafe)
                .ThenByDescending(item => item.Character.ResolvedCharacterName is not null
                    ? item.Character.Confidence
                    : item.Line.Confidence)
                .ToArray();
            var best = ordered[0];
            var bestScore = best.Character.ResolvedCharacterName is not null ? best.Character.Confidence : best.Line.Confidence;
            var conflicting = ordered.Skip(1).Any(item =>
                !string.Equals(item.Character.ResolvedCharacterName, best.Character.ResolvedCharacterName, StringComparison.Ordinal) ||
                item.IsExplicitEmpty != best.IsExplicitEmpty);
            var secondScore = ordered.Skip(1).Select(item => item.Character.ResolvedCharacterName is not null
                    ? item.Character.Confidence
                    : item.Line.Confidence)
                .DefaultIfEmpty(0)
                .Max();
            if (conflicting && (!best.Character.IsAutoApplySafe || bestScore - secondScore < .08))
            {
                slots[group.Key].SlotState = SmartBpRecognizedSlotState.Unknown;
                slots[group.Key].RecognitionReason = $"conflicting OCR candidates in fixed visual slot {group.Key}";
                diagnostics.Add($"{field}[{group.Key}]: conflicting candidates retained as Unknown; no candidate was shifted.");
                continue;
            }

            if (best.IsExplicitEmpty && best.Character.ResolvedCharacterName is null)
            {
                slots[group.Key].SlotState = SmartBpRecognizedSlotState.Empty;
                slots[group.Key].CharacterName = "未选择";
                slots[group.Key].RecognitionConfidence = best.Line.Confidence;
                slots[group.Key].IsAutoApplySafe = best.Line.Confidence >= .90;
                slots[group.Key].RecognitionReason = "explicit OCR empty-slot label mapped by fixed geometry";
                slots[group.Key].BoundingBox = best.Line.BoundingBox;
            }
            else
            {
                slots[group.Key].CharacterName = best.Character.ResolvedCharacterName!;
                ApplyRecognitionMetadata(slots[group.Key], best.Character, best.Line.BoundingBox);
            }
            diagnostics.Add($"{field}: centerX={best.Line.CenterX:0.0}/{regionWidth:0.0} -> fixed slot {group.Key}.");
        }
        diagnostics.Add($"{field}: parsed [{string.Join(", ", slots.Select(slot => $"{slot.Index}={slot.CharacterName}"))}]");
        return new() { Phase = "未知", TargetField = field, Slots = slots };
    }

    private static int ResolveBanVisualSlot(double centerX, double regionWidth, int count)
    {
        if (regionWidth <= 0 || count <= 1)
            return 0;

        if (count != 4)
            return Math.Clamp((int)(centerX / (regionWidth / count)), 0, count - 1);

        var normalizedX = Math.Clamp(centerX / regionWidth, 0, 1);
        double[] anchors = [.19, .38, .57, .76];
        return anchors
            .Select((anchor, index) => (Index: index, Distance: Math.Abs(anchor - normalizedX)))
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Index)
            .First().Index;
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

        // 全局快照和已选定角色阶段均可能包含 talent/extra 行，只把角色行和选手 ID 行写入业务槽位。
        if (mode is SmartBpPickedSurOcrParseMode.GlobalSnapshot or
            SmartBpPickedSurOcrParseMode.DistributeChara or
            SmartBpPickedSurOcrParseMode.SurvivorTalent)
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
            if (resolved.ResolvedCharacterName != null)
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
                if (resolved.ResolvedCharacterName != null)
                    diagnostics.Add($"picked_sur ignored lower-row character candidate row={sr.PhysicalIndex} raw={line.Text} result={resolved.ResolvedCharacterName} reason=below-player-id-row");
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
        if (resolved.ResolvedCharacterName != null)
        {
            slot.CharacterName = resolved.ResolvedCharacterName!;
            ApplyRecognitionMetadata(slot, resolved, item.BoundingBox);
        }
        else if (SmartBpBusinessStateParser.IsUnselected(item.Text))
        {
            slot.CharacterName = "未选择";
            slot.SlotState = SmartBpRecognizedSlotState.Empty;
            slot.RecognitionConfidence = item.Confidence;
            slot.IsAutoApplySafe = item.Confidence >= .90;
            slot.RecognitionReason = "explicit OCR empty-slot label";
            slot.BoundingBox = item.BoundingBox;
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
                if (resolved.ResolvedCharacterName != null)
                    diagnostics.Add($"picked_sur ignored lower-row character candidate row={rowIndex} raw={line.Text} result={resolved.ResolvedCharacterName} reason=below-player-id-row");
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
            .Where(item => item.Character.ResolvedCharacterName != null)
            .OrderBy(item => item.Line.CenterY)
            .ThenBy(item => item.Line.CenterX)
            .FirstOrDefault();
        var slot = new SmartBpRecognizedPlayerCharacterSlot { Index = 0, CharacterName = "未选择" };
        if (anchor != null)
        {
            slot.CharacterName = anchor.Character.ResolvedCharacterName!;
            ApplyRecognitionMetadata(slot, anchor.Character, anchor.Line.BoundingBox);
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
            .Where(line => resolver.ResolveCharacterFromLine(line.Text, camp, anchorIndex, line.Provider).ResolvedCharacterName == null)
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
        SmartBpNormalizedCharacter character,
        Rect? boundingBox = null)
    {
        slot.SlotState = SmartBpRecognizedSlotState.Selected;
        slot.RecognitionConfidence = character.Confidence;
        slot.IsAutoApplySafe = character.IsAutoApplySafe && character.Confidence >= .90;
        slot.RecognitionReason = $"matchMode={character.MatchMode}; {character.RecognitionReason ?? character.Warnings.FirstOrDefault()}";
        slot.BoundingBox = boundingBox;
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
